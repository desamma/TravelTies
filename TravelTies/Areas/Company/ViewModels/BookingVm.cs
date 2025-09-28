using System.ComponentModel.DataAnnotations;

namespace TravelTies.Areas.Company.ViewModels
{
    public class BookingFilterVm
    {
        public string? Q { get; set; }            // tên/email KH, tên tour, mã booking
        public string Status { get; set; } = "all"; // all|confirmed|pending
        [DataType(DataType.Date)] public DateTime? From { get; set; }
        [DataType(DataType.Date)] public DateTime? To { get; set; }
    }

    public class BookingListItemVm
    {
        public Guid TicketId { get; set; }
        public string Code => TicketId.ToString("N")[..6].ToUpper(); // BK00xx
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";
        public string TourName { get; set; } = "";
        public string Destination { get; set; } = "";
        public DateOnly TourDate { get; set; }
        public int People { get; set; } = 1;
        public bool IsPayed { get; set; }
        public decimal Total { get; set; }
        public string Picture { get; set; } = "";
    }

    public class BookingIndexVm
    {
        public BookingFilterVm Filter { get; set; } = new();
        // KPI
        public int Total { get; set; }
        public int Confirmed { get; set; }
        public int Pending { get; set; }
        public decimal TotalRevenue { get; set; }
        // List
        public List<BookingListItemVm> Items { get; set; } = new();
    }
}
