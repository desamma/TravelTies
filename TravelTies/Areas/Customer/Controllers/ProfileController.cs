using System.Security.Claims;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using TravelTies.Areas.Customer.ViewModels;
using Utilities.Utils;

namespace TravelTies.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = "customer")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly CloudinaryUploader _uploader;

        public ProfileController(ApplicationDbContext db, CloudinaryUploader uploader)
        {
            _db = db;
            _uploader = uploader;
        }

        private bool TryGetUserId(out Guid userId)
        {
            userId = Guid.Empty;
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(uid, out userId);
        }

        // GET: /Customer/Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var user = await _db.Users
                .Include(u => u.Tickets)
                .Include(u => u.Ratings)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            var vm = new CustomerProfileVm
            {
                Id = user.Id,
                FullName = user.UserName,
                Avatar = user.UserAvatar,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                JoinDate = user.CreatedDate ?? DateTime.Now,
                TotalTickets = user.Tickets.Count,
                AvgRating = user.Ratings.Any() ? user.Ratings.Average(r => r.Score) : 0,
                RatingCount = user.Ratings.Count
            };

            return View(vm);
        }

        // GET: /Customer/Profile/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            var vm = new CustomerProfileEditVm
            {
                Id = user.Id,
                FullName = user.UserName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Avatar = user.UserAvatar
            };

            return View(vm);
        }

        // POST: /Customer/Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerProfileEditVm vm)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (vm.Id != userId) return BadRequest("Sai định danh người dùng.");
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            // Upload avatar mới nếu có
            if (vm.AvatarFile != null && vm.AvatarFile.Length > 0)
            {
                var uploaded = await _uploader.UploadMediaAsync(vm.AvatarFile);
                if (uploaded == "Unsupported file type")
                {
                    ModelState.AddModelError(nameof(vm.AvatarFile), "Định dạng file không hỗ trợ (jpg, png, gif, webp, ...).");
                    return View(vm);
                }
                if (!string.IsNullOrWhiteSpace(uploaded))
                {
                    user.UserAvatar = uploaded; // lưu URL Cloudinary
                    vm.Avatar = uploaded;       // giữ cho view hiển thị lại
                }
            }
            else
            {
                // Không upload mới: giữ URL cũ (nếu có hidden field Avatar trong form)
                if (!string.IsNullOrWhiteSpace(vm.Avatar))
                    user.UserAvatar = vm.Avatar;
            }

            // Cập nhật thông tin cơ bản
            user.UserName = vm.FullName;
            user.PhoneNumber = vm.PhoneNumber;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
