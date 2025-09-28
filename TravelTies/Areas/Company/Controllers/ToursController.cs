using System.Security.Claims;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using TravelTies.Areas.Company.ViewModels;
using Utilities.Utils;

namespace TravelTies.Areas.Company.Controllers
{
    [Area("Company")]
    [Authorize(Roles = "company")]
    public class ToursController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly CloudinaryUploader _uploader;

        public ToursController(ApplicationDbContext db, CloudinaryUploader uploader)
        {
            _db = db;
            _uploader = uploader;
        }

        private bool TryGetCompanyId(out Guid companyId)
        {
            companyId = Guid.Empty;
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(uid, out companyId);
        }

        // GET: /Company/Tours?status=all&q=...
        [HttpGet]
        public async Task<IActionResult> Index(string status = "all", string? q = null)
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();

            var query = _db.Tours
                .Include(t => t.Tickets)
                .Include(t => t.Ratings)
                .Where(t => t.CompanyId == companyId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(t => t.TourName.Contains(q) || t.Destination.Contains(q));
            }

            // Tính trạng thái
            var list = await query
                .OrderByDescending(t => t.TourStartDate)
                .ToListAsync();

            string GetStatus(Tour t)
            {
                var booked = t.Tickets?.Count ?? 0;
                if (booked >= t.NumberOfPassenger && t.NumberOfPassenger > 0) return "Đầy chỗ";
                return "Đang hoạt động";
            }

            // Lọc theo status (all/active/draft/full) — giả sử draft = chưa có tên/giá hợp lệ (ví dụ)
            if (status == "active")
                list = list.Where(t => GetStatus(t) == "Đang hoạt động").ToList();
            else if (status == "full")
                list = list.Where(t => GetStatus(t) == "Đầy chỗ").ToList();
            else if (status == "draft")
                list = list.Where(t => string.IsNullOrWhiteSpace(t.TourName) || t.Price <= 0).ToList();

            var mapped = list.Select(t => new TourListItemVm
            {
                TourId = t.TourId,
                TourName = t.TourName,
                Destination = t.Destination,
                Start = t.TourStartDate,
                End = t.TourEndDate,
                NumberOfPassenger = t.NumberOfPassenger,
                Booked = t.Tickets?.Count ?? 0,
                Views = t.Views,
                Price = t.Price,
                Rating = t.Ratings?.Any() == true ? t.Ratings.Average(r => r.Score) : 0,
                Status = GetStatus(t),
                Picture = t.Picture
            }).ToList();

            // Counters
            int total = await _db.Tours.CountAsync(t => t.CompanyId == companyId);
            int full = await _db.Tours
                .Where(t => t.CompanyId == companyId)
                .CountAsync(t => (t.Tickets.Count >= t.NumberOfPassenger) && t.NumberOfPassenger > 0);
            int active = total - full;
            int draft = await _db.Tours
                .Where(t => t.CompanyId == companyId)
                .CountAsync(t => string.IsNullOrWhiteSpace(t.TourName) || t.Price <= 0);

            var vm = new TourIndexVm
            {
                Filter = new TourFilterVm { Status = status, Q = q },
                Items = mapped,
                Total = total,
                Active = active,
                Draft = draft,
                Full = full
            };
            return View(vm);
        }

        // GET: /Company/Tours/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();

            var t = await _db.Tours
                .Include(x => x.Tickets)
                .Include(x => x.Ratings)
                .FirstOrDefaultAsync(x => x.TourId == id && x.CompanyId == companyId);

            if (t == null) return NotFound();

            var vm = new TourListItemVm
            {
                TourId = t.TourId,
                TourName = t.TourName,
                Destination = t.Destination,
                Start = t.TourStartDate,
                End = t.TourEndDate,
                NumberOfPassenger = t.NumberOfPassenger,
                Booked = t.Tickets?.Count ?? 0,
                Views = t.Views,
                Price = t.Price,
                Rating = t.Ratings?.Any() == true ? t.Ratings.Average(r => r.Score) : 0,
                Status = (t.Tickets?.Count ?? 0) >= t.NumberOfPassenger && t.NumberOfPassenger > 0 ? "Đầy chỗ" : "Đang hoạt động",
                Picture = t.Picture
            };

            return View(vm);
        }

        // GET: /Company/Tours/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new TourFormVm
            {
                TourStartDate = DateOnly.FromDateTime(DateTime.Today),
                TourEndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                NumberOfPassenger = 20,
                HotelStars = 3
            });
        }

        // POST: /Company/Tours/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TourFormVm vm)
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();

            if (!ModelState.IsValid) return View(vm);

            string? pictureUrl = vm.Picture; // fallback nếu không upload file
            if (vm.PictureFile != null && vm.PictureFile.Length > 0)
            {
                var uploaded = await _uploader.UploadMediaAsync(vm.PictureFile);
                if (uploaded == "Unsupported file type")
                {
                    ModelState.AddModelError(nameof(vm.PictureFile), "Định dạng file không hỗ trợ.");
                    return View(vm);
                }
                pictureUrl = uploaded ?? vm.Picture;
            }

            var entity = new Tour
            {
                TourId = Guid.NewGuid(),
                CompanyId = companyId,
                TourName = vm.TourName,
                Description = vm.Description,
                Destination = vm.Destination,
                NumberOfPassenger = vm.NumberOfPassenger,
                TourStartDate = vm.TourStartDate,
                TourEndDate = vm.TourEndDate,
                TourScheduleDescription = vm.TourScheduleDescription,
                Discount = vm.Discount,
                HotelStars = vm.HotelStars,
                Price = vm.Price,
                Picture = pictureUrl,
                SupportTourMatching = vm.SupportTourMatching,
                Commission = vm.Commission
            };

            _db.Tours.Add(entity);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Company/Tours/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();

            var t = await _db.Tours.FirstOrDefaultAsync(x => x.TourId == id && x.CompanyId == companyId);
            if (t == null) return NotFound();

            var vm = new TourFormVm
            {
                TourId = t.TourId,
                TourName = t.TourName,
                Description = t.Description,
                Destination = t.Destination,
                NumberOfPassenger = t.NumberOfPassenger,
                TourStartDate = t.TourStartDate,
                TourEndDate = t.TourEndDate,
                TourScheduleDescription = t.TourScheduleDescription,
                Discount = t.Discount,
                HotelStars = t.HotelStars,
                Price = t.Price,
                Picture = t.Picture,
                SupportTourMatching = t.SupportTourMatching,
                Commission = t.Commission
            };
            return View(vm);
        }

        // POST: /Company/Tours/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, TourFormVm vm)
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();
            if (id != vm.TourId) return BadRequest();
            if (!ModelState.IsValid) return View(vm);

            var t = await _db.Tours.FirstOrDefaultAsync(x => x.TourId == id && x.CompanyId == companyId);
            if (t == null) return NotFound();

            // Upload file nếu có, nếu không giữ Picture hiện tại (vm.Picture là hidden/readonly)
            if (vm.PictureFile != null && vm.PictureFile.Length > 0)
            {
                var uploaded = await _uploader.UploadMediaAsync(vm.PictureFile);
                if (uploaded == "Unsupported file type")
                {
                    ModelState.AddModelError(nameof(vm.PictureFile), "Định dạng file không hỗ trợ.");
                    return View(vm);
                }
                t.Picture = uploaded ?? t.Picture;
            }
            else
            {
                // nếu người dùng xóa input URL (nếu có), vẫn giữ ảnh cũ
                if (!string.IsNullOrWhiteSpace(vm.Picture))
                    t.Picture = vm.Picture;
            }

            t.TourName = vm.TourName;
            t.Description = vm.Description;
            t.Destination = vm.Destination;
            t.NumberOfPassenger = vm.NumberOfPassenger;
            t.TourStartDate = vm.TourStartDate;
            t.TourEndDate = vm.TourEndDate;
            t.TourScheduleDescription = vm.TourScheduleDescription;
            t.Discount = vm.Discount;
            t.HotelStars = vm.HotelStars;
            t.Price = vm.Price;
            t.SupportTourMatching = vm.SupportTourMatching;
            t.Commission = vm.Commission;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Company/Tours/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();

            var t = await _db.Tours.FirstOrDefaultAsync(x => x.TourId == id && x.CompanyId == companyId);
            if (t == null) return NotFound();

            _db.Tours.Remove(t);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
