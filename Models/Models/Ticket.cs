using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models;

public class Ticket
{
    public Guid TicketId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Owner { get; set; } = string.Empty;
    
    [DataType(DataType.Date)]
    public DateOnly TourDate { get; set; }
    
    [DataType(DataType.DateTime)]
    public DateTime TransactionDateTime { get; set; }
    
    [Display(Name = "Phone Number")]
    [DataType(DataType.PhoneNumber)]
    [StringLength(11, ErrorMessage = "The phone number must be at most 11 digits.")]
    [RegularExpression(@"^\d{1,11}$", ErrorMessage = "The phone number must only contain digits")]
    public string? PhoneNumber { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal TicketPrice { get; set; }
    
    public int NumberOfSeats { get; set; }

    public int? GroupNumber { get; set; }
    
    // lưu orderCode trả về từ PayOS
    public long? PaymentOrderCode { get; set; }

    public bool IsPayed { get; set; } = false;

    [DataType(DataType.DateTime)]
    public DateTime CancellationDateTime { get; set; }
    
    public Guid? UserId { get; set; }
    [ValidateNever]
    public virtual User User { get; set; }

    public Guid? TourId { get; set; }
    [ValidateNever]
    public virtual Tour Tour { get; set; }
}