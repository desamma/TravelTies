using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TravelTies.Areas.Company.ViewModels
{
    public class TourFilterVm
    {
        public string Status { get; set; } = "all";
        public string? Q { get; set; }
    }

    public class TourFormVm
    {
        public Guid? TourId { get; set; }

        [Required, MaxLength(200)]
        public string TourName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required, MaxLength(200)]
        public string Destination { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int NumberOfPassenger { get; set; }

        [DataType(DataType.Date)]
        public DateOnly TourStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateOnly TourEndDate { get; set; }

        [MaxLength(2000)]
        public string? TourScheduleDescription { get; set; }

        [Range(0, 100)]
        public decimal Discount { get; set; }

        [Range(1, 5)]
        public int HotelStars { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        // Lưu URL hiện tại (nếu có)
        public string? Picture { get; set; }

        // File chọn từ máy để upload lên Cloudinary
        public IFormFile? PictureFile { get; set; }

        public bool SupportTourMatching { get; set; }
        [Range(0, 100)]
        public double Commission { get; set; }
    }

    public class TourListItemVm
    {
        public Guid TourId { get; set; }
        public string TourName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly Start { get; set; }
        public DateOnly End { get; set; }
        public int NumberOfPassenger { get; set; }
        public int Booked { get; set; }
        public int Views { get; set; }
        public decimal Price { get; set; }
        public double Rating { get; set; }
        public string Status { get; set; } = "Đang hoạt động";
        public int OccupancyPercent => NumberOfPassenger > 0 ? (int)Math.Round(100.0 * Booked / NumberOfPassenger) : 0;
        public string? Picture { get; set; }
    }

    public class TourIndexVm
    {
        public TourFilterVm Filter { get; set; } = new();
        public List<TourListItemVm> Items { get; set; } = new();

        public int Total { get; set; }
        public int Active { get; set; }
        public int Draft { get; set; }
        public int Full { get; set; }
    }
}
