namespace TravelTies.Areas.Admin.ViewModels;

public class ActivityItem
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = "fa-info-circle";
    public string Color { get; set; } = "text-gray-600";
}