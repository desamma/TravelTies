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
    private const int PageSize = 6;
    private readonly IRatingRepository _ratingRepo;
    private readonly ITicketRepository _ticketRepo;

    public TourController(ITourRepository tourRepo, IRatingRepository ratingRepo, ITicketRepository ticketRepo)
    {
        _tourRepo = tourRepo;
        _ratingRepo = ratingRepo;
        _ticketRepo = ticketRepo;
    }

    public async Task<IActionResult> Index(string search, string category, DateTime? departureDate, decimal? minPrice, decimal? maxPrice,
        int page = 1)
    {
        var query = _tourRepo.GetAllQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.TourName.Contains(search) || t.Destination.Contains(search));

        if (!string.IsNullOrEmpty(category) && category != "Tất cả")
            query = query.Where(t => t.Category == category);

        if (departureDate.HasValue)
            query = query.Where(t => t.TourStartDate <= DateOnly.FromDateTime(departureDate.Value) && t.TourEndDate >= DateOnly.FromDateTime(departureDate.Value));

        if (minPrice.HasValue)
            query = query.Where(t => t.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(t => t.Price <= maxPrice.Value);

        var totalItems = await query.CountAsync();
        var tours = await query
            .OrderByDescending(t => t.Views)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewBag.Categories = await _tourRepo.GetAllQueryable().Select(t => t.Category).Distinct().ToListAsync();
        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.DepartureDate = departureDate?.ToString("yyyy-MM-dd");
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);

        return View(tours);
    }

    public async Task<IActionResult> Detail(Guid id)
    {
        if (id == Guid.Empty) return NotFound();

        // Load tour with navigation (if needed)
        var tour = await _tourRepo.GetAllQueryable(t => t.TourId == id)
            .Include(t => t.Ratings)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (tour == null) return NotFound();

        // Ratings for this tour
        var ratings = await _ratingRepo.GetAllQueryable(r => r.TourId == id)
            .OrderByDescending(r => r.CommentDate)
            .AsNoTracking()
            .ToListAsync();

        var avg = ratings.Any() ? ratings.Average(r => r.Score) : 0;
        var reviewsCount = ratings.Count;

        bool isAuthenticated = User?.Identity?.IsAuthenticated ?? false;
        bool canRate = false;

        if (isAuthenticated)
        {
            // get user id from claims (adjust claim type if different)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userGuid))
            {
                // Check if user has a paid ticket for this tour
                canRate = await _ticketRepo.GetAllQueryable(t => t.TourId == id && t.UserId == userGuid && t.IsPayed)
                    .AnyAsync();
            }
        }

        var numberOfTicket = _ticketRepo.GetAllQueryable(t => t.TourId == id).Count();

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

    // POST rating submission
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRating(Guid tourId, int score, string? comment)
    {
        if (tourId == Guid.Empty || score < 1 || score > 5)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userGuid))
            return Forbid();

        // Check if user bought & paid ticket for this tour
        var bought = await _ticketRepo.GetAllQueryable(t => t.TourId == tourId && t.UserId == userGuid && t.IsPayed)
            .AnyAsync();

        if (!bought)
        {
            TempData["RatingError"] = "Bạn chỉ có thể đánh giá sau khi mua vé cho tour này.";
            return RedirectToAction("Detail", new { id = tourId });
        }

        var rating = new Rating
        {
            RatingId = Guid.NewGuid(),
            Score = score,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CommentDate = DateTime.UtcNow,
            TourId = tourId,
            UserId = userGuid
        };

        var added = await _ratingRepo.AddAsync(rating);
        if (!added)
        {
            TempData["RatingError"] = "Không thể lưu đánh giá. Vui lòng thử lại.";
        }

        return RedirectToAction("Detail", new { id = tourId });
    }
}