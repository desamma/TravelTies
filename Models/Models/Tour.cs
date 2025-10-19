using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models;

public class Tour
{
    public Guid TourId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string TourName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? Category { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? About { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "Number of passengers must be at least 1.")]
    public int NumberOfPassenger { get; set; }

    [DataType(DataType.Date)]
    public DateOnly TourStartDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly TourEndDate { get; set; }
    
    [MaxLength(2000)]
    public string? TourScheduleDescription { get; set; }

    [Range(0, int.MaxValue)]
    public int Sale { get; set; } = 0;

    [Required]
    [MaxLength(200)]
    public string Destination { get; set; } = string.Empty;
    
    [Range(0, 5)]
    public int HotelStars { get; set; } // hotel star rating
    
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public string? Picture { get; set; } // path or URL

    [Range(0, 100, ErrorMessage = "Discount percentage must be between 0 and 100.")]
    public decimal Discount { get; set; }  // % discount

    public bool SupportTourMatching { get; set; } // T/F
    
    [Range(0, 100, ErrorMessage = "Commission percentage must be between 0 and 100.")]
    public double Commission { get; set; } // % commission, set by admin

    [Range(0, int.MaxValue)]
    public int Views { get; set; } = 0;

    [Range(0, 100)]
    public double ConversionRate { get; set; } // View/Ticket
    
    public Guid? CompanyId { get; set; }
    [ValidateNever]
    public virtual User Company { get; set; }
    
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    
    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    
    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
}