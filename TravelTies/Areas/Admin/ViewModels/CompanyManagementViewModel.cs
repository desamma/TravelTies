using Models.Models;

namespace TravelTies.Areas.Admin.ViewModels;

public class CompanyManagementViewModel
{
    public List<User> Companies { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string? SearchTerm { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}