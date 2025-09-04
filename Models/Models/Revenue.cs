using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models;

public class Revenue
{
    public Guid RevenueId { get; set; }
    
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Revenue must be a positive number.")]
    public decimal Amount { get; set; }
    
    [Required]
    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
    public int Month { get; set; }
    
    [Required]
    [Range(2000, 2100, ErrorMessage = "Year must be reasonable.")]
    public int Year { get; set; }
    
    public Guid? CompanyId { get; set; }
    [ValidateNever]
    public virtual User Company { get; set; }
}