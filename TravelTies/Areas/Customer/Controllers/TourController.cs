using System.Security.Claims;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using TravelTies.Areas.Customer.Models;

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
