using System.Security.Claims;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Models.Models;
using TravelTies.Hubs;
using TravelTies.AI; // <-- thêm

namespace TravelTies.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class ChatController : Controller
{
    private readonly IChatRepository _chatRepo;
    private readonly IUserRepository _userRepo;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IAiService _ai;              // <-- thêm

    private const string HiddenConvKey = "chat:hidden:conversations";
    private const string HiddenMsgKey = "chat:hidden:messages";

    public ChatController(
        IChatRepository chatRepo,
        IUserRepository userRepo,
        IHubContext<ChatHub> hub,
        IAiService ai)                         // <-- thêm
    {
        _chatRepo = chatRepo;
        _userRepo = userRepo;
        _hub = hub;
        _ai = ai;                              // <-- thêm
    }

    private Guid Me() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private HashSet<Guid> GetHiddenSet(string key)
    {
        var s = HttpContext.Session.GetString(key);
        return string.IsNullOrWhiteSpace(s) ? new() : new(s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse));
    }
    private void SaveHiddenSet(string key, HashSet<Guid> set)
        => HttpContext.Session.SetString(key, string.Join(',', set));

    // tạo user ảo AI nếu chưa có
    private async Task EnsureAiUserAsync()
    {
        var ai = await _userRepo.GetAllQueryable(u => u.Id == AiAssistant.Id).FirstOrDefaultAsync();
        if (ai == null)
        {
            var u = new User
            {
                Id = AiAssistant.Id,
                UserName = AiAssistant.DisplayName,
                Email = "ai@travelties.local",
                IsCompany = true,
                CreatedDate = DateTime.UtcNow
            };
            await _userRepo.AddAsync(u);
        }
    }

    // map đối tác -> thời điểm tin gần nhất
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

    // Customer: sidebar CHỈ đại lý, search cũng chỉ đại lý; luôn PIN AI trên đầu
    private async Task<List<User>> GetPartnersAsync(Guid me, string? search)
    {
        var hidden = GetHiddenSet(HiddenConvKey);
        var lastMap = await GetLastTsMapAsync(me);

        IQueryable<User> q;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = _userRepo.GetAllQueryable(u =>
                    u.IsCompany && u.Id != me &&
                    (u.UserName.Contains(term) || (u.Email != null && u.Email.Contains(term))));
        }
        else
        {
            var counterpartIds = lastMap.Keys;
            // chỉ lấy các đại lý đã có lịch sử
            q = _userRepo.GetAllQueryable(u => u.IsCompany && counterpartIds.Contains(u.Id));
        }

        var users = await q.Where(u => !hidden.Contains(u.Id))
                           .OrderBy(u => u.UserName)
                           .Take(50)
                           .ToListAsync();

        // PIN AI lên đầu list
        var ai = users.FirstOrDefault(u => u.Id == AiAssistant.Id);
        if (ai == null)
        {
            users.Insert(0, new User
            {
                Id = AiAssistant.Id,
                UserName = AiAssistant.DisplayName,
                Email = "ai@travelties.local",
                IsCompany = true
            });
        }
        else
        {
            users.Remove(ai);
            users.Insert(0, ai);
        }

        // sau AI, sắp theo thời gian mới nhất rồi theo tên
        var ordered = new[] { users.First() }
            .Concat(users.Skip(1)
                .OrderByDescending(u => lastMap.TryGetValue(u.Id, out var ts) ? ts : DateTime.MinValue)
                .ThenBy(u => u.UserName))
            .ToList();

        return ordered;
    }

    [HttpGet("/customer/chat")]
    public async Task<IActionResult> Index(string? q, Guid? with)
    {
        await EnsureAiUserAsync(); // <-- thêm

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
        ViewBag.Partners = partners;
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

        // 1) lưu tin nhắn của user
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

        var payload = new { chat.ChatId, chat.Message, chat.Timestamp, chat.IsUserChat, chat.SenderId, chat.ReceiverId };
        await _hub.Clients.Group(me.ToString()).SendAsync("ReceiveMessage", payload);
        await _hub.Clients.Group(peerId.ToString()).SendAsync("ReceiveMessage", payload);

        // 2) nếu đang chat với AI → gọi AI và auto-reply
        if (peerId == AiAssistant.Id)
        {
            var reply = await _ai.AskAsync(message, me);

            var aiMsg = new Chat
            {
                ChatId = Guid.NewGuid(),
                Message = reply,
                Timestamp = DateTime.UtcNow,
                IsUserChat = false,
                SenderId = AiAssistant.Id,
                ReceiverId = me
            };
            await _chatRepo.AddAsync(aiMsg);

            var aiPayload = new { aiMsg.ChatId, aiMsg.Message, aiMsg.Timestamp, aiMsg.IsUserChat, aiMsg.SenderId, aiMsg.ReceiverId };
            await _hub.Clients.Group(me.ToString()).SendAsync("ReceiveMessage", aiPayload);
        }

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
