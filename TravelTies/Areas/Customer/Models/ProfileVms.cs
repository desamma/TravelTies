using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TravelTies.Areas.Customer.ViewModels
{
    public class CustomerProfileVm
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }

        // thông tin tham khảo (không bắt buộc có trong DB, tùy UI)
        public DateTime JoinDate { get; set; } = DateTime.Now;
        public int TotalTickets { get; set; }
        public double AvgRating { get; set; }
        public int RatingCount { get; set; }
    }

    public class CustomerProfileEditVm
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

        [Display(Name = "Chọn ảnh đại diện mới")]
        public IFormFile? AvatarFile { get; set; }
    }
}
