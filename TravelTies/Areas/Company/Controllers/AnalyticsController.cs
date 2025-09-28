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
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AnalyticsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(uid, out var companyId)) return Unauthorized();

            var now = DateTime.Now;
            var month = now.Month;
            var year = now.Year;

            // Doanh thu tháng hiện tại
            var thisMonthRevenue = await _db.Revenues
                .Where(r => r.CompanyId == companyId && r.Month == month && r.Year == year)
                .SumAsync(r => (decimal?)r.Amount) ?? 0m;

            // Số booking tháng hiện tại (Ticket không có CreatedAt, dùng CancellationDateTime như mốc đặt vé)
            var thisMonthBookings = await _db.Tickets
                .Where(t => t.Tour.CompanyId == companyId &&
                            t.CancellationDateTime.Month == month &&
                            t.CancellationDateTime.Year == year)
                .CountAsync();

            // Rating trung bình
            var avgRating = await _db.Ratings
                .Where(r => r.Tour.CompanyId == companyId)
                .AverageAsync(r => (double?)r.Score) ?? 0d;

            // Conversion
            var totalBookings = await _db.Tickets.Where(t => t.Tour.CompanyId == companyId).CountAsync();
            var totalViews = await _db.Tours.Where(t => t.CompanyId == companyId).SumAsync(t => (int?)t.Views) ?? 0;
            var conversion = totalViews > 0 ? (double)totalBookings / totalViews * 100d : 0d;

            // Doanh thu 12 tháng gần nhất
            var revenueTrend = new List<decimal>();
            var labels = new List<string>();
            for (int i = 11; i >= 0; i--)
            {
                var dt = now.AddMonths(-i);
                var m = dt.Month;
                var y = dt.Year;
                var rev = await _db.Revenues
                    .Where(r => r.CompanyId == companyId && r.Month == m && r.Year == y)
                    .SumAsync(r => (decimal?)r.Amount) ?? 0m;
                labels.Add($"{m:00}/{y}");
                revenueTrend.Add(rev);
            }

            // Top tour
            var tours = await _db.Tours
                .Where(t => t.CompanyId == companyId)
                .Include(t => t.Tickets)
                .Include(t => t.Ratings)
                .ToListAsync();

            var topTours = tours.Select(t => new TourPerfVm
            {
                TourId = t.TourId,
                TourName = t.TourName,
                Destination = t.Destination,
                Capacity = t.NumberOfPassenger,
                Booked = t.Tickets?.Count ?? 0,
                Price = t.Price,
                Rating = t.Ratings?.Any() == true ? t.Ratings.Average(r => r.Score) : 0,
                Views = t.Views
            }).OrderByDescending(x => x.Revenue).Take(5).ToList();

            var vm = new AnalyticsVm
            {
                ThisMonthRevenue = thisMonthRevenue,
                ThisMonthBookings = thisMonthBookings,
                AvgRating = Math.Round(avgRating, 2),
                ConversionRate = Math.Round(conversion, 2),
                RevenueLabels = labels,
                RevenueTrend = revenueTrend,
                TopTours = topTours
            };

            return View(vm);
        }
    }
}
