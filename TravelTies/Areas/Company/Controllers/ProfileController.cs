using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataAccess;
using TravelTies.Areas.Company.ViewModels;
using Utilities.Utils; // CloudinaryUploader
using Models.Models;

namespace TravelTies.Areas.Company.Controllers
{
    [Area("Company")]
    [Authorize(Roles = "company")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly CloudinaryUploader _uploader;

        public ProfileController(ApplicationDbContext db, CloudinaryUploader uploader)
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

        // GET: /Company/Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();

            var user = await _db.Users
                .Include(u => u.Tours)
                .Include(u => u.Ratings)
                .Include(u => u.Tickets)
                .FirstOrDefaultAsync(u => u.Id == companyId);

            if (user == null) return NotFound();

            var vm = new ProfileVm
            {
                Id = user.Id,
                FullName = user.UserName,
                Avatar = user.UserAvatar,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Address = user.ContactInfo,
                AgencyName = string.IsNullOrWhiteSpace(user.Description) ? "Doanh nghiệp của bạn" : user.Description,
                JoinDate = user.CreatedDate ?? DateTime.Now,

                // Demo stats (tuỳ bạn map theo dữ liệu thật)
                TotalBookings = user.Tickets.Count,
                AvgRating = user.Ratings.Any() ? user.Ratings.Average(r => r.Score) : 0,
                RatingCount = user.Ratings.Count,
                CompletionRate = 100,
                Level = "Pro",
                Achievements = new()
                {
                    ("🏆","Đối tác uy tín","Hoàn thành 100% booking đã nhận"),
                    ("💼","Doanh nghiệp chuẩn","Thông tin hồ sơ đầy đủ"),
                    ("🌟","Chất lượng cao","Điểm đánh giá trung bình ≥ 4.5")
                }
            };

            return View(vm);
        }

        // GET: /Company/Profile/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == companyId);
            if (user == null) return NotFound();

            var vm = new ProfileEditVm
            {
                Id = user.Id,
                FullName = user.UserName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Avatar = user.UserAvatar,
                Description = user.Description,
                ContactInfo = user.ContactInfo
            };
            return View(vm);
        }

        // POST: /Company/Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileEditVm vm)
        {
            if (!TryGetCompanyId(out var companyId)) return Unauthorized();
            if (companyId != vm.Id) return BadRequest("Sai định danh người dùng.");
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == companyId);
            if (user == null) return NotFound();

            // Nếu có file, upload lên Cloudinary
            if (vm.AvatarFile != null && vm.AvatarFile.Length > 0)
            {
                var uploaded = await _uploader.UploadMediaAsync(vm.AvatarFile);
                if (uploaded == "Unsupported file type")
                {
                    ModelState.AddModelError(nameof(vm.AvatarFile), "Định dạng file không hỗ trợ (jpg, png, gif, webp...).");
                    return View(vm);
                }
                if (!string.IsNullOrWhiteSpace(uploaded))
                {
                    user.UserAvatar = uploaded;     // LƯU URL Cloudinary vào DB
                    vm.Avatar = uploaded;           // để form giữ lại hiển thị khi ModelState lỗi
                }
            }
            else
            {
                // Không upload mới: giữ Avatar hiện tại từ input hidden/readonly (nếu bạn render)
                if (!string.IsNullOrWhiteSpace(vm.Avatar))
                    user.UserAvatar = vm.Avatar;
            }

            // Cập nhật các trường khác
            user.UserName = vm.FullName;
            user.PhoneNumber = vm.PhoneNumber;
            user.Description = vm.Description;
            user.ContactInfo = vm.ContactInfo;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
