using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;              // <-- THÊM DÒNG NÀY
using DataAccess;
using Models.Models;
using TravelTies.Areas.Company.ViewModels;

namespace TravelTies.Areas.Company.Controllers
{
    [Area("Company")]
    [Authorize(Roles = "company")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;

            // Lấy userId từ claim NameIdentifier (Identity mặc định)
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(uid, out var companyId))
                return Unauthorized(); // hoặc RedirectToAction("Login","Account")

            var totalTours = await _db.Tours.CountAsync(t => t.CompanyId == companyId);
            var activeTours = await _db.Tours.CountAsync(t =>
                t.CompanyId == companyId && t.TourStartDate >= DateOnly.FromDateTime(now.AddDays(-7)));
            var totalBookings = await _db.Tickets.CountAsync(t => t.Tour.CompanyId == companyId);

            var monthlyRevenue = await _db.Revenues
                .Where(r => r.CompanyId == companyId && r.Month == now.Month && r.Year == now.Year)
                .SumAsync(r => (decimal?)r.Amount) ?? 0m;

            // Nếu Tour chưa có cột Views thì để 0 an toàn
            var sumViews = await _db.Tours.Where(t => t.CompanyId == companyId)
                .SumAsync(t => (int?)t.Views) ?? 0;
            var conversion = sumViews > 0 ? (double)totalBookings / sumViews * 100d : 0d;

            var recentTours = await _db.Tours
                .Where(t => t.CompanyId == companyId)
                .OrderByDescending(t => t.TourStartDate)
                .Select(t => new TourSummaryVm
                {
                    TourId = t.TourId,
                    TourName = t.TourName,
                    CurrentTickets = t.Tickets.Count,
                    Capacity = t.NumberOfPassenger,
                    Price = t.Price,
                    Status = t.TourEndDate >= DateOnly.FromDateTime(now) ? "Đang hoạt động" : "Đã kết thúc"
                })
                .Take(5).AsNoTracking().ToListAsync();

            var recentActivities = await _db.Tickets
                .Where(t => t.Tour.CompanyId == companyId)
                .OrderByDescending(t => t.CancellationDateTime)
                .Select(t => $"Khách hàng {t.User.UserName} đã đặt tour {t.Tour.TourName}")
                .Take(5).AsNoTracking().ToListAsync();

            var vm = new DashboardVm
            {
                TotalTours = totalTours,
                ActiveTours = activeTours,
                TotalBookings = totalBookings,
                MonthlyRevenue = monthlyRevenue,
                RecentTours = recentTours,
                RecentActivities = recentActivities
            };

            ViewBag.ConversionRate = Math.Round(conversion, 2);
            return View(vm);
        }
    }
}
