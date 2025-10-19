namespace TravelTies.Areas.Admin.ViewModels;

public class DashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalCompanies { get; set; }
    public decimal UserGrowthPercentage { get; set; }
    public List<decimal> MonthlyRevenue { get; set; } = new();
    public List<string> MonthLabels { get; set; } = new();
    public List<ActivityItem> RecentActivities { get; set; } = new();
}