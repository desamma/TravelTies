using System.Security.Claims;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Models.Models;
using TravelTies.Hubs;

namespace TravelTies.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class ChatController : Controller
{
    private readonly IChatRepository _chatRepo;
    private readonly IUserRepository _userRepo;
    private readonly IHubContext<ChatHub> _hub;

    private const string HiddenConvKey = "companychat:hidden:conversations";
    private const string HiddenMsgKey = "companychat:hidden:messages";

    public ChatController(IChatRepository chatRepo, IUserRepository userRepo, IHubContext<ChatHub> hub)
    {
        _chatRepo = chatRepo;
        _userRepo = userRepo;
        _hub = hub;
    }

    private Guid Me() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private HashSet<Guid> GetHiddenSet(string key)
    {
        var s = HttpContext.Session.GetString(key);
        return string.IsNullOrWhiteSpace(s) ? new() : new(s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse));
    }
    private void SaveHiddenSet(string key, HashSet<Guid> set)
        => HttpContext.Session.SetString(key, string.Join(',', set));

    private async Task<Dictionary<Guid, DateTime>> GetLastTsMapAsync(Guid me)
    {
        var raw = await _chatRepo.GetAllQueryable(c => c.SenderId == me || c.ReceiverId == me)
            .Select(c => new {
                PeerId = c.SenderId == me ? c.ReceiverId : c.SenderId,
                c.Timestamp
            })
            .Where(x => x.PeerId != null)
            .ToListAsync();

        return raw
            .GroupBy(x => x.PeerId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(z => z.Timestamp));
    }

    // Company:
    // - Có q => chỉ tìm trong đại lý (IsCompany=true)
    // - Không q => liệt kê lịch sử (cả khách & đại lý)
    // Company: có q -> tìm toàn bộ đại lý; không q -> lịch sử (đại lý & khách)
    private async Task<List<User>> GetPartnersAsync(Guid me, string? search)
    {
        var hidden = GetHiddenSet(HiddenConvKey);
        var lastMap = await GetLastTsMapAsync(me);

        IQueryable<User> q;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = _userRepo.GetAllQueryable(u =>
                    u.IsCompany
                    && u.Id != me
                    && u.UserName.ToLower().Contains(term));
        }
        else
        {
            var counterpartIds = lastMap.Keys;
            q = _userRepo.GetAllQueryable(u => counterpartIds.Contains(u.Id));
        }

        var users = await q.Where(u => !hidden.Contains(u.Id)).ToListAsync();

        return users
            .OrderByDescending(u => lastMap.TryGetValue(u.Id, out var ts) ? ts : DateTime.MinValue)
            .ThenBy(u => u.UserName)
            .Take(50)
            .ToList();
    }


    [HttpGet("/company/chat")]
    public async Task<IActionResult> Index(string? q, Guid? with)
    {
        var me = Me();
        var partners = await GetPartnersAsync(me, q);

        Guid? peer = with;
        if (peer == null && partners.Any()) peer = partners.First().Id;

        var hiddenMsgs = GetHiddenSet(HiddenMsgKey);
        var messages = new List<Chat>();
        if (peer.HasValue)
        {
            messages = await _chatRepo.GetAllQueryable(c =>
                    (c.SenderId == me && c.ReceiverId == peer) ||
                    (c.SenderId == peer && c.ReceiverId == me))
                .Where(c => !hiddenMsgs.Contains(c.ChatId))
                .OrderBy(c => c.Timestamp)
                .AsNoTracking()
                .ToListAsync();
        }

        ViewBag.Me = me;
        ViewBag.Peer = peer;
        ViewBag.Search = q;
        ViewBag.Partners = partners; // <-- thêm
        return View(messages);
    }

    [HttpGet]
    public async Task<IActionResult> Thread(Guid peerId)
    {
        var me = Me();
        var hiddenMsgs = GetHiddenSet(HiddenMsgKey);
        var msgs = await _chatRepo.GetAllQueryable(c =>
                (c.SenderId == me && c.ReceiverId == peerId) ||
                (c.SenderId == peerId && c.ReceiverId == me))
            .Where(c => !hiddenMsgs.Contains(c.ChatId))
            .OrderBy(c => c.Timestamp)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.Me = me;
        ViewBag.Peer = peerId;
        return PartialView("_Thread", msgs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(Guid peerId, string message)
    {
        if (peerId == Guid.Empty || string.IsNullOrWhiteSpace(message)) return BadRequest();
        var me = Me();

        var chat = new Chat
        {
            ChatId = Guid.NewGuid(),
            Message = message.Trim(),
            Timestamp = DateTime.UtcNow,
            IsUserChat = true,
            SenderId = me,
            ReceiverId = peerId
        };
        if (!await _chatRepo.AddAsync(chat)) return StatusCode(500);

        var payload = new
        {
            chat.ChatId,
            chat.Message,
            chat.Timestamp,
            chat.IsUserChat,
            chat.SenderId,
            chat.ReceiverId
        };
        await _hub.Clients.Group(me.ToString()).SendAsync("ReceiveMessage", payload);
        await _hub.Clients.Group(peerId.ToString()).SendAsync("ReceiveMessage", payload);
        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recall(Guid chatId, bool both)
    {
        if (chatId == Guid.Empty) return BadRequest();
        var me = Me();

        if (both)
        {
            var msg = await _chatRepo.GetAllQueryable(c => c.ChatId == chatId).FirstOrDefaultAsync();
            if (msg == null) return NotFound();
            if (msg.SenderId != me) return Forbid();

            msg.Message = "🗑 Tin nhắn đã bị thu hồi";
            if (!await _chatRepo.UpdateAsync(msg)) return StatusCode(500);

            var peer = msg.SenderId == me ? msg.ReceiverId : msg.SenderId;
            await _hub.Clients.Group(me.ToString()).SendAsync("Recalled", new { chatId });
            if (peer.HasValue) await _hub.Clients.Group(peer.Value.ToString()).SendAsync("Recalled", new { chatId });
            return Ok(new { success = true, both = true });
        }
        else
        {
            var hidden = GetHiddenSet(HiddenMsgKey);
            hidden.Add(chatId);
            SaveHiddenSet(HiddenMsgKey, hidden);
            return Ok(new { success = true, both = false });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult HideConversation(Guid peerId)
    {
        if (peerId == Guid.Empty) return BadRequest();
        var set = GetHiddenSet(HiddenConvKey);
        set.Add(peerId);
        SaveHiddenSet(HiddenConvKey, set);
        return Ok(new { success = true });
    }
}
