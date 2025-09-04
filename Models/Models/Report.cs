using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models;

public class Report
{
    public Guid ReportId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [DataType(DataType.DateTime)]
    public DateTime Date { get; set; } = DateTime.UtcNow;
    
    public Guid? TourId { get; set; }
    [ValidateNever]
    public virtual Tour Tour { get; set; }

    public Guid? UserId { get; set; }
    [ValidateNever]
    public virtual User User { get; set; }
}