using System.ComponentModel.DataAnnotations;

namespace TravelTies.Areas.Admin.ViewModels;

public class CreateCompanyViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Company Created Date")]
    public DateOnly? CompanyCreatedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [Display(Name = "Avatar")]
    public IFormFile? AvatarFile { get; set; }

    [MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(5000)]
    [Display(Name = "Contact Information")]
    public string ContactInfo { get; set; } = string.Empty;
}