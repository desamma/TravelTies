using System.Globalization;
using System.Security.Claims;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using TravelTies.Areas.Customer.Models;

namespace TravelTies.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class BookingController : Controller
{
    private readonly ITourRepository _tourRepo;
    private readonly ITicketRepository _ticketRepo; // or ITicketRepository if present
    private readonly UserManager<User> _userManager;
    
    // coupon map - Todo:create in db or remove 
    private static readonly Dictionary<string,int> CouponMap = new()
    {
        ["WELCOME10"] = 10,
        ["SUMMER20"] = 20,
        ["FLASH50"] = 50
    };

    public BookingController(ITourRepository tourRepo, ITicketRepository ticketRepo, UserManager<User> userManager)
    {
        _tourRepo = tourRepo;
        _ticketRepo = ticketRepo;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(Guid tourId, DateTime tourDate, int quantity, decimal ticketPrice, string owner, string phoneNumber)
    {
        // Basic validations
        var tour = await _tourRepo.GetAsync(t => t.TourId == tourId);
        if (tour == null) {
            TempData["BookingError"] = "Tour không tồn tại.";
            return RedirectToAction("Detail", "Tour", new { id = tourId });
        }

        // Check date range
        var tourStart = new DateTime(tour.TourStartDate.Year, tour.TourStartDate.Month, tour.TourStartDate.Day);
        var tourEnd = new DateTime(tour.TourEndDate.Year, tour.TourEndDate.Month, tour.TourEndDate.Day);
        if (tourDate.Date < tourStart.Date || tourDate.Date > tourEnd.Date)
        {
            TempData["BookingError"] = "Ngày khởi hành không hợp lệ.";
            return RedirectToAction("Detail", "Tour", new { id = tourId });
        }

        if (quantity < 1)
        {
            TempData["BookingError"] = "Số người không hợp lệ.";
            return RedirectToAction("Detail", "Tour", new { id = tourId });
        }

        // (Optional) Check remaining tickets logic if you track Tickets and capacity
        // e.g. var remaining = ComputeRemainingSeats(tourId, tourDate); if(quantity > remaining) ...

        // Build ticket
        var user = await _userManager.GetUserAsync(User);
        var ticket = new Ticket
        {
            TicketId = Guid.NewGuid(),
            Owner = owner,
            PhoneNumber = phoneNumber,
            TourDate = DateOnly.FromDateTime(tourDate.Date),
            NumberOfSeats = quantity,
            TicketPrice = ticketPrice,
            IsPayed = false,
            UserId = user?.Id,
            TourId = tour.TourId,
            CancellationDateTime = tourDate.Date.AddHours(-12) // 12 hours before tour date
        };

        var added = await _ticketRepo.AddAsync(ticket);
        if (!added)
        {
            TempData["BookingError"] = "Tạo vé thất bại, vui lòng thử lại.";
            return RedirectToAction("Detail", "Tour", new { id = tourId });
        }

        TempData["BookingSuccess"] = "Tạo vé thành công. Vui lòng thanh toán hoặc kiểm tra giỏ hàng.";
        // Redirect to cart page or booking summary
        return RedirectToAction("Cart", "Booking");
    }
    
    [HttpGet]
    public async Task<IActionResult> Cart()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // get tickets for current user which are not paid yet (change condition as required)
        var query = _ticketRepo.GetAllQueryable(t => t.UserId == user.Id && !t.IsPayed, asNoTracking: true)
                               .Include(t => t.Tour);

        var tickets = await query.ToListAsync();

        var vm = new BookingCartViewModel();
        foreach (var t in tickets)
        {
            var firstImage = string.Empty;
            if (!string.IsNullOrEmpty(t.Tour?.Picture))
            {
                var parts = t.Tour.Picture.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) firstImage = parts[0].Trim();
            }

            vm.Tickets.Add(new TicketViewModel
            {
                TicketId = t.TicketId,
                TourId = t.TourId,
                TourName = t.Tour?.TourName ?? "(Tour)",
                PictureFirstUrl = string.IsNullOrEmpty(firstImage) ? null : firstImage,
                TourDate = t.TourDate,
                NumberOfSeats = t.NumberOfSeats,
                TicketPrice = t.TicketPrice,
                Destination = t.Tour?.Destination,
                IsPayed = t.IsPayed
            });
        }

        vm.Subtotal = vm.Tickets.Sum(x => x.TicketPrice);

        // Restore applied coupon from TempData (if present)
        if (TempData.TryGetValue("CouponCode", out var couponObj) && couponObj is string appliedCode)
        {
            vm.CouponCode = appliedCode;
            if (TempData.TryGetValue("CouponPercent", out var percObj) && int.TryParse(percObj?.ToString(), out var perc))
            {
                vm.DiscountPercent = perc;
            }

            // Keep for next request so the view keeps coupon data
            TempData.Keep("CouponCode");
            TempData.Keep("CouponPercent");
        }

        vm.DiscountAmount = Math.Round(vm.Subtotal * vm.DiscountPercent / 100m, 0);
        vm.FinalTotal = vm.Subtotal - vm.DiscountAmount;

        return View(vm);
    }

    // POST: /Booking/ApplyCoupon
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApplyCoupon(string couponCode)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            TempData["CouponError"] = "Vui lòng nhập mã giảm giá.";
            return RedirectToAction(nameof(Cart));
        }

        couponCode = couponCode.Trim().ToUpperInvariant();
        if (CouponMap.TryGetValue(couponCode, out var percent))
        {
            TempData["CouponCode"] = couponCode;
            TempData["CouponPercent"] = percent.ToString(CultureInfo.InvariantCulture);
            TempData["CouponSuccess"] = $"Mã {couponCode} áp dụng: {percent}%";
        }
        else
        {
            TempData["CouponError"] = "Mã giảm giá không hợp lệ.";
        }

        return RedirectToAction(nameof(Cart));
    }

    // POST: /Booking/CancelTicket
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelTicket(Guid ticketId)
    {
        var success = await _ticketRepo.DeleteAsync(ticketId);
        if (!success)
        {
            TempData["CartError"] = "Huỷ vé thất bại.";
        }
        // On success or failure return to home page per your requirement
        return RedirectToAction("Index", "Home");
    }

    // POST: /Booking/Checkout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // collect unpaid tickets for user
        var tickets = await _ticketRepo.GetAllQueryable(t => t.UserId == user.Id && !t.IsPayed)
                                       .ToListAsync();

        if (!tickets.Any())
        {
            TempData["CartError"] = "Không có vé nào để thanh toán.";
            return RedirectToAction(nameof(Cart));
        }

        decimal subtotal = tickets.Sum(t => t.TicketPrice);
        int percent = 0;
        if (TempData.TryGetValue("CouponPercent", out var percObj) && int.TryParse(percObj?.ToString(), out var p))
        {
            percent = p;
            // keep coupon for redirection or future use
            TempData.Keep("CouponPercent");
            TempData.Keep("CouponCode");
        }

        var discountAmount = Math.Round(subtotal * percent / 100m, 0);
        var final = subtotal - discountAmount;

        // Build comma-separated ticket ids
        var ticketIds = string.Join(',', tickets.Select(t => t.TicketId.ToString()));

        // Redirect to PaymentController.Start with query params (or call payment creation)
        return RedirectToAction("Start", "Payment", new { ticketIds = ticketIds, amount = final });
    }
    
    // GET: /Customer/Booking/MyTickets
    [HttpGet("MyTickets")]
    public async Task<IActionResult> MyTickets()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr))
            return Challenge();

        Guid userId = Guid.Parse(userIdStr);

        var tickets = await _ticketRepo
            .GetAllQueryable(t => t.UserId == userId, asNoTracking: true)
            .Include(t => t.Tour)
            .OrderByDescending(t => t.TicketId)
            .ToListAsync();

        var vm = tickets.Select(t => new TicketListItemViewModel
        {
            TicketId = t.TicketId,
            TourName = t.Tour?.TourName ?? "—",
            TourPicture = t.Tour?.Picture,
            TourDate = t.TourDate,
            NumberOfSeats = t.NumberOfSeats,
            TicketPrice = t.TicketPrice,
            IsPayed = t.IsPayed,
            PaymentOrderCode = t.PaymentOrderCode
        }).ToList();

        return View(vm);
    }

    // GET: /Customer/Booking/Details/{id}
    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var ticketQuery = _ticketRepo
            .GetAllQueryable(t => t.TicketId == id, asNoTracking: true)
            .Include(t => t.Tour)
            .Include(t => t.User);

        var ticket = await ticketQuery.FirstOrDefaultAsync();
        if (ticket == null) return NotFound();

        // only owner or admin can view
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr))
            return Challenge();

        var currentUserId = Guid.Parse(userIdStr);
        var isOwner = ticket.UserId.HasValue && ticket.UserId.Value == currentUserId;
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Administrator");

        if (!isOwner && !isAdmin)
            return Forbid();

        var vm = new TicketDetailsViewModel
        {
            TicketId = ticket.TicketId,
            TourName = ticket.Tour?.TourName ?? "—",
            TourDestination = ticket.Tour?.Destination ?? string.Empty,
            TourPicture = ticket.Tour?.Picture,
            TourDate = ticket.TourDate,
            NumberOfSeats = ticket.NumberOfSeats,
            TicketPrice = ticket.TicketPrice,
            IsPayed = ticket.IsPayed,
            PaymentOrderCode = ticket.PaymentOrderCode,
            CancellationDateTime = ticket.CancellationDateTime,
            OwnerName = ticket.Owner,
            OwnerEmail = ticket.User?.Email,
            OwnerPhone = ticket.PhoneNumber
        };

        return View(vm);
    }

    //TODO: Optional: A delete action to cancel a ticket (soft/hard) - keep basic for now
    /*[HttpPost("Cancel/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var ticket = await _ticketRepo.GetAsync(t => t.TicketId == id);
        if (ticket == null) return NotFound();

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr)) return Challenge();
        var currentUserId = Guid.Parse(userIdStr);

        var isOwner = ticket.UserId.HasValue && ticket.UserId.Value == currentUserId;
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Administrator");

        if (!isOwner && !isAdmin) return Forbid();

        // Hard-delete for example; consider refund/cancellation policy in real app
        await _ticketRepo.DeleteAsync(ticket.TicketId);
        TempData["Message"] = "Vé đã được huỷ.";
        return RedirectToAction("MyTickets");
    }*/
}