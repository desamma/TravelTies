using Models.Models;

namespace TravelTies.Areas.Admin.ViewModels;

public class UserManagementViewModel
{
    public List<User> Users { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string? SearchTerm { get; set; }
    public string? SelectedGender { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}