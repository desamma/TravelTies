using System.Security.Claims;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using TravelTies.Areas.Customer.Models;
using System;                // để dùng Math, DateOnly
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TravelTies.Areas.Customer.Controllers;

[Area("Customer")]
public class TourController : Controller
{
    private readonly ITourRepository _tourRepo;
    private readonly IRatingRepository _ratingRepo;
    private readonly ITicketRepository _ticketRepo;

    private const int PageSize = 6;

    public TourController(
        ITourRepository tourRepo,
        IRatingRepository ratingRepo,
        ITicketRepository ticketRepo)
    {
        _tourRepo = tourRepo;
        _ratingRepo = ratingRepo;
        _ticketRepo = ticketRepo;
    }

    // ======================== LIST / FILTER ========================
    public async Task<IActionResult> Index(
        string? search,
        string? category,
        DateTime? departureDate,
        decimal? minPrice,
        decimal? maxPrice,
        int page = 1)
    {
        var query = _tourRepo.GetAllQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.TourName.Contains(search) || t.Destination.Contains(search));

        if (!string.IsNullOrWhiteSpace(category) && category != "Tất cả")
            query = query.Where(t => t.Category == category);

        if (departureDate.HasValue)
        {
            var d = DateOnly.FromDateTime(departureDate.Value);
            query = query.Where(t => t.TourStartDate <= d && t.TourEndDate >= d);
        }

        if (minPrice.HasValue) query = query.Where(t => t.Price >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(t => t.Price <= maxPrice.Value);

        var totalItems = await query.CountAsync();
        var tours = await query
            .OrderByDescending(t => t.Views)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewBag.Categories = await _tourRepo.GetAllQueryable()
            .Select(t => t.Category)
            .Distinct()
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.DepartureDate = departureDate?.ToString("yyyy-MM-dd");
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);

        return View(tours);
    }

    // ======================== ƯU ĐÃI (DEALS) ========================
    // Clone Index nhưng chỉ hiển thị tour có Discount > 0, sắp xếp giảm dần theo Discount rồi Price
    public async Task<IActionResult> Deals(
        string? search,
        string? category,
        DateTime? departureDate,
        decimal? minPrice,
        decimal? maxPrice,
        int page = 1)
    {
        var query = _tourRepo.GetAllQueryable()
            .Where(t => t.Discount > 0);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.TourName.Contains(search) || t.Destination.Contains(search));

        if (!string.IsNullOrWhiteSpace(category) && category != "Tất cả")
            query = query.Where(t => t.Category == category);

        if (departureDate.HasValue)
        {
            var d = DateOnly.FromDateTime(departureDate.Value);
            query = query.Where(t => t.TourStartDate <= d && t.TourEndDate >= d);
        }

        if (minPrice.HasValue) query = query.Where(t => t.Price >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(t => t.Price <= maxPrice.Value);

        var totalItems = await query.CountAsync();

        var tours = await query
            .OrderByDescending(t => t.Discount)
            .ThenByDescending(t => t.Price)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.Categories = await _tourRepo.GetAllQueryable()
            .Select(t => t.Category)
            .Distinct()
            .ToListAsync();

        ViewBag.Title = "Tours Ưu đãi";
        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.DepartureDate = departureDate?.ToString("yyyy-MM-dd");
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);

        return View("Deals", tours);
    }

    // ======================== NỔI BẬT (FEATURED) ========================
    // Kết hợp chất lượng (Bayesian rating) + phổ biến (log số vé đã thanh toán)
    public async Task<IActionResult> Featured(
        string? search,
        string? category,
        DateTime? departureDate,
        decimal? minPrice,
        decimal? maxPrice,
        int page = 1)
    {
        // μ: trung bình rating toàn site
        double mu = await _ratingRepo.GetAllQueryable().AnyAsync()
            ? await _ratingRepo.GetAllQueryable().AverageAsync(r => (double)r.Score)
            : 4.2; // fallback

        const int C = 20; // prior cho Bayes

        var baseQ = _tourRepo.GetAllQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            baseQ = baseQ.Where(t => t.TourName.Contains(search) || t.Destination.Contains(search));

        if (!string.IsNullOrWhiteSpace(category) && category != "Tất cả")
            baseQ = baseQ.Where(t => t.Category == category);

        if (departureDate.HasValue)
        {
            var d = DateOnly.FromDateTime(departureDate.Value);
            baseQ = baseQ.Where(t => t.TourStartDate <= d && t.TourEndDate >= d);
        }

        if (minPrice.HasValue) baseQ = baseQ.Where(t => t.Price >= minPrice.Value);
        if (maxPrice.HasValue) baseQ = baseQ.Where(t => t.Price <= maxPrice.Value);

        // Lấy dữ liệu cần thiết
        var data = await baseQ
            .Select(t => new
            {
                Tour = t,
                RatingAvg = t.Ratings.Any() ? t.Ratings.Average(r => (double)r.Score) : 0.0,
                RatingCount = t.Ratings.Count(),
                OrdersPaid = t.Tickets.Count(k => k.IsPayed)
            })
            .AsNoTracking()
            .ToListAsync();

        // Chuẩn hoá Popularity bằng log(1+OrdersPaid)
        double maxLogOrders = data.Count > 0
            ? data.Select(x => Math.Log(1 + x.OrdersPaid)).DefaultIfEmpty(0).Max()
            : 0.0;

        var scored = data.Select(x =>
        {
            // Quality [0..1] từ Bayes rating
            double bayes = (x.RatingCount * x.RatingAvg + C * mu) / (x.RatingCount + C);
            // đảm bảo bayes trong [1,5] nếu thiếu dữ liệu
            bayes = Math.Min(5.0, Math.Max(1.0, bayes));
            double quality = (bayes - 1.0) / 4.0;

            // Popularity [0..1] từ log orders
            double popRaw = Math.Log(1 + x.OrdersPaid);
            double popNorm = maxLogOrders > 0 ? (popRaw / maxLogOrders) : 0.0;

            // Hợp nhất (ưu tiên chất lượng)
            double score = 0.6 * quality + 0.4 * popNorm;

            return new { x.Tour, Score = score, Quality = quality, Popularity = popNorm };
        })
        .OrderByDescending(z => z.Score)
        .ThenByDescending(z => z.Popularity)
        .ThenByDescending(z => z.Quality)
        .ToList();

        var totalItems = scored.Count;
        var pageItems = scored
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(z => z.Tour)
            .ToList();

        ViewBag.Categories = await _tourRepo.GetAllQueryable()
            .Select(t => t.Category)
            .Distinct()
            .ToListAsync();

        ViewBag.Title = "Tours Nổi bật";
        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.DepartureDate = departureDate?.ToString("yyyy-MM-dd");
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);

        return View("Featured", pageItems);
    }

    // ======================== DETAIL + RATINGS ========================
    public async Task<IActionResult> Detail(Guid id)
    {
        if (id == Guid.Empty) return NotFound();

        // Load tour (kèm Ratings cho sẵn) 
        var tour = await _tourRepo.GetAllQueryable(t => t.TourId == id)
            .Include(t => t.Ratings)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (tour == null) return NotFound();

        // Load danh sách rating của tour + kèm User để view hiển thị tên/avatar
        var ratings = await _ratingRepo.GetAllQueryable(r => r.TourId == id)
            .Include(r => r.User)
            .OrderByDescending(r => r.CommentDate)
            .AsNoTracking()
            .ToListAsync();

        var avg = ratings.Any() ? ratings.Average(r => r.Score) : 0;
        var reviewsCount = ratings.Count;

        bool isAuthenticated = User?.Identity?.IsAuthenticated ?? false;
        bool canRate = false;

        if (isAuthenticated)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var userGuid))
            {
                // Chỉ được đánh giá nếu có vé đã thanh toán
                canRate = await _ticketRepo
                    .GetAllQueryable(t => t.TourId == id && t.UserId == userGuid && t.IsPayed)
                    .AnyAsync();
            }
        }

        var numberOfTicket = await _ticketRepo.GetAllQueryable(t => t.TourId == id).CountAsync();

        var vm = new TourDetailViewModel
        {
            Tour = tour,
            Ratings = ratings,
            AverageRating = Math.Round(avg, 2),
            ReviewsCount = reviewsCount,
            IsAuthenticated = isAuthenticated,
            CanRate = canRate,
            RemainingTickets = tour.NumberOfPassenger - numberOfTicket
        };

        return View(vm);
    }

    // ======================== CREATE RATING ========================
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRating(Guid tourId, int score, string? comment)
    {
        if (tourId == Guid.Empty || score < 1 || score > 5)
            return BadRequest();

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userGuid))
            return Forbid();

        // Kiểm tra đã mua vé & thanh toán
        var bought = await _ticketRepo
            .GetAllQueryable(t => t.TourId == tourId && t.UserId == userGuid && t.IsPayed)
            .AnyAsync();

        if (!bought)
        {
            TempData["RatingError"] = "Bạn chỉ có thể đánh giá sau khi mua vé cho tour này.";
            return RedirectToAction(nameof(Detail), new { id = tourId });
        }

        // (Tuỳ chọn) enforce 1 đánh giá / người / tour
        // var existed = await _ratingRepo.GetAllQueryable(r => r.TourId == tourId && r.UserId == userGuid).AnyAsync();
        // if (existed) { TempData["RatingError"] = "Bạn đã đánh giá tour này."; return RedirectToAction(nameof(Detail), new { id = tourId }); }

        var rating = new Rating
        {
            RatingId = Guid.NewGuid(),
            Score = score,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            // Dù model có default DateTime.Now, ta lưu UTC để nhất quán
            CommentDate = DateTime.UtcNow,
            TourId = tourId,      // model là Guid?, gán trực tiếp Guid OK
            UserId = userGuid     // model là Guid?, gán trực tiếp Guid OK
        };

        var added = await _ratingRepo.AddAsync(rating);
        if (!added)
        {
            TempData["RatingError"] = "Không thể lưu đánh giá. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Detail), new { id = tourId });
    }

    // ======================== EDIT RATING ========================
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRating(Guid ratingId, Guid tourId, int score, string? comment)
    {
        if (ratingId == Guid.Empty || tourId == Guid.Empty || score < 1 || score > 5)
            return BadRequest();

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userGuid))
            return Forbid();

        var rating = await _ratingRepo
            .GetAllQueryable(r => r.RatingId == ratingId && r.TourId == tourId)
            .FirstOrDefaultAsync();

        if (rating == null) return NotFound();

        // Chỉ chủ comment mới được sửa
        if (rating.UserId != userGuid) return Forbid();

        rating.Score = score;
        rating.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        rating.CommentDate = DateTime.UtcNow; // cập nhật time sửa

        var ok = await _ratingRepo.UpdateAsync(rating); // UpdateAsync nhận entity
        if (!ok)
        {
            TempData["RatingError"] = "Không thể cập nhật bình luận. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Detail), new { id = tourId });
    }

    // ======================== DELETE RATING ========================
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRating(Guid ratingId, Guid tourId)
    {
        if (ratingId == Guid.Empty || tourId == Guid.Empty)
            return BadRequest();

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userGuid))
            return Forbid();

        var rating = await _ratingRepo
            .GetAllQueryable(r => r.RatingId == ratingId && r.TourId == tourId)
            .FirstOrDefaultAsync();

        if (rating == null) return NotFound();

        // Chỉ chủ comment mới được xoá
        if (rating.UserId != userGuid) return Forbid();

        // ⭐ Quan trọng: Delete theo GUID, KHÔNG truyền cả object
        var ok = await _ratingRepo.DeleteAsync(rating.RatingId);
        if (!ok)
        {
            TempData["RatingError"] = "Không thể xoá bình luận. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Detail), new { id = tourId });
    }
}
