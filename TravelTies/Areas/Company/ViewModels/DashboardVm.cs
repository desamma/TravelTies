namespace TravelTies.Areas.Company.ViewModels
{
    public class DashboardVm
    {
        public int TotalTours { get; set; }
        public int ActiveTours { get; set; }
        public int TotalBookings { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public double AvgRating { get; set; }
        public List<TourSummaryVm> RecentTours { get; set; } = new();
        public List<string> RecentActivities { get; set; } = new();
    }

    public class TourSummaryVm
    {
        public Guid TourId { get; set; }
        public string TourName { get; set; } = string.Empty;
        public int CurrentTickets { get; set; }
        public int Capacity { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
