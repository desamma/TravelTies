using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models;

public class Ticket
{
    public Guid TicketId { get; set; }

    public int? GroupNumber { get; set; }

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