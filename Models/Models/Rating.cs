using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models;

public class Rating
{
    public Guid RatingId { get; set; }
    
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Score { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }

    public string? Picture { get; set; } // optional photo

    [DataType(DataType.DateTime)]
    public DateTime CommentDate { get; set; } = DateTime.Now;
    
    public Guid? TourId { get; set; }
    [ValidateNever]
    public virtual Tour Tour { get; set; }

    public Guid? UserId { get; set; }
    [ValidateNever]
    public virtual User User { get; set; }
}