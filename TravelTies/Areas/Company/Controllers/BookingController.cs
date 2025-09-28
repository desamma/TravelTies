using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DataAccess;
using TravelTies.Areas.Company.ViewModels;

namespace TravelTies.Areas.Company.Controllers
{
    [Area("Company")]
    [Authorize(Roles = "company")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _db;
        public BookingController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index(string? q, string status = "all", DateTime? from = null, DateTime? to = null)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(uid, out var companyId)) return Unauthorized();

            // Query tất cả ticket thuộc tour của company
            var query = _db.Tickets
                .Include(t => t.User)
                .Include(t => t.Tour)
                .Where(t => t.Tour.CompanyId == companyId)
                .AsQueryable();

            // Lọc theo từ khóa
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(t =>
                    (t.User.UserName.Contains(q) || (t.User.Email ?? "").Contains(q)) ||
                    t.Tour.TourName.Contains(q) ||
                    t.TicketId.ToString().Contains(q));
            }

            // Lọc theo ngày tour (sử dụng TourStartDate trong Tour)
            if (from.HasValue) query = query.Where(t => t.Tour.TourStartDate >= DateOnly.FromDateTime(from.Value.Date));
            if (to.HasValue) query = query.Where(t => t.Tour.TourStartDate <= DateOnly.FromDateTime(to.Value.Date));

            // Lọc trạng thái
            if (status == "confirmed") query = query.Where(t => t.IsPayed);
            if (status == "pending") query = query.Where(t => !t.IsPayed);

            var list = await query
                .OrderByDescending(t => t.Tour.TourStartDate)
                .ThenByDescending(t => t.IsPayed)
                .ToListAsync();

            // KPI
            var total = await _db.Tickets.CountAsync(t => t.Tour.CompanyId == companyId);
            var confirmed = await _db.Tickets.CountAsync(t => t.Tour.CompanyId == companyId && t.IsPayed);
            var pending = total - confirmed;
            var revenue = await _db.Tickets
                                .Where(t => t.Tour.CompanyId == companyId && t.IsPayed)
                                .SumAsync(t => (decimal?)((t.Tour.Price) * (t.GroupNumber ?? 1))) ?? 0m; // tổng tiền = Price * số người

            var vm = new BookingIndexVm
            {
                Filter = new BookingFilterVm { Q = q, Status = status, From = from, To = to },
                Total = total,
                Confirmed = confirmed,
                Pending = pending,
                TotalRevenue = revenue,
                Items = list.Select(t => new BookingListItemVm
                {
                    TicketId = t.TicketId,
                    CustomerName = t.User.UserName ?? "",
                    CustomerEmail = t.User.Email ?? "",
                    TourName = t.Tour.TourName,
                    Destination = t.Tour.Destination,
                    TourDate = t.Tour.TourStartDate,
                    People = t.GroupNumber ?? 1,          // schema hiện tại không có field số người, dùng GroupNumber nếu có
                    IsPayed = t.IsPayed,
                    Total = t.Tour.Price * (t.GroupNumber ?? 1),
                    Picture = t.Tour.Picture ?? ""
                }).ToList()
            };

            return View(vm);
        }

        // Xác nhận thanh toán (đặt là đã thanh toán)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(Guid id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(uid, out var companyId)) return Unauthorized();

            var ticket = await _db.Tickets.Include(x => x.Tour)
                .FirstOrDefaultAsync(x => x.TicketId == id && x.Tour.CompanyId == companyId);
            if (ticket == null) return NotFound();

            ticket.IsPayed = true;
            await _db.SaveChangesAsync();

            TempData["ok"] = "Đã xác nhận thanh toán.";
            return RedirectToAction(nameof(Index));
        }

        // Hủy booking (xóa ticket) — nếu muốn soft-delete, bạn có thể thêm cờ riêng.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(uid, out var companyId)) return Unauthorized();

            var ticket = await _db.Tickets.Include(x => x.Tour)
                .FirstOrDefaultAsync(x => x.TicketId == id && x.Tour.CompanyId == companyId);
            if (ticket == null) return NotFound();

            _db.Tickets.Remove(ticket);
            await _db.SaveChangesAsync();

            TempData["ok"] = "Đã hủy booking.";
            return RedirectToAction(nameof(Index));
        }
    }
}
