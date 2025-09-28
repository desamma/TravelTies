using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TravelTies.Areas.Customer.Models
{
    public class ProfileVm
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Address { get; set; } = string.Empty; // dùng ContactInfo nếu bạn muốn
        public string AgencyName { get; set; } = string.Empty; // có thể lấy từ Description
        public DateTime JoinDate { get; set; } = DateTime.Now;

        // Các field minh hoạ UI (có thể map từ dữ liệu thật hoặc tính toán)
        public int TotalBookings { get; set; }
        public double AvgRating { get; set; }
        public int RatingCount { get; set; }
        public int CompletionRate { get; set; } = 100;
        public string Level { get; set; } = "Pro";

        public List<(string icon, string title, string desc)> Achievements { get; set; } = new();
    }

    public class ProfileEditVm
    {
        public Guid Id { get; set; }

        [Display(Name = "Họ và tên")]
        [Required, MaxLength(50)]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Ảnh đại diện (URL hiện tại)")]
        public string? Avatar { get; set; }   // URL đang lưu trong DB

        // File upload mới từ máy
        [Display(Name = "Chọn ảnh đại diện mới")]
        public IFormFile? AvatarFile { get; set; }

        // “Tên doanh nghiệp” và “Địa chỉ liên hệ” tái dụng các field mô tả sẵn của User
        [Display(Name = "Tên doanh nghiệp")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ liên hệ")]
        public string ContactInfo { get; set; } = string.Empty;
    }
}
