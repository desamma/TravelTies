namespace TravelTies.Areas.Company.ViewModels
{
    public class TourPerfVm
    {
        public Guid TourId { get; set; }
        public string TourName { get; set; } = "";
        public string Destination { get; set; } = "";
        public int Capacity { get; set; }
        public int Booked { get; set; }
        public decimal Price { get; set; }
        public decimal Revenue => Price * Booked;
        public int Occupancy => Capacity > 0 ? (int)Math.Round((double)Booked / Capacity * 100) : 0;
        public double Rating { get; set; }
        public int Views { get; set; }
    }

    public class AnalyticsVm
    {
        // KPI
        public decimal ThisMonthRevenue { get; set; }
        public int ThisMonthBookings { get; set; }
        public double AvgRating { get; set; }
        public double ConversionRate { get; set; }

        // Biểu đồ doanh thu
        public List<string> RevenueLabels { get; set; } = new();   // ["01/2025", "02/2025", ...]
        public List<decimal> RevenueTrend { get; set; } = new();   // [1200000, 950000, ...]

        // Top tour
        public List<TourPerfVm> TopTours { get; set; } = new();
    }
}
