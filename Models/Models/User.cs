using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models
{
    public class User : IdentityUser<Guid>
    {
        [MaxLength(50)]
        [Required]
        public override string UserName { get; set; }
        
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateOnly? UserDOB { get; set; }
        
        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        [StringLength(11, ErrorMessage = "The phone number must be at most 11 digits.")]
        [RegularExpression(@"^\d{1,11}$", ErrorMessage = "The phone number must only contain digits")]
        public override string? PhoneNumber { get; set; }
        
        [Display(Name = "Gender")]
        public bool Gender { get; set; }
        
        [ValidateNever] 
        public string? UserAvatar { get; set; }
        
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime? CreatedDate { get; set; } = DateTime.Now;
        
        public bool IsBanned { get; set; }
        
        public bool IsCompany { get; set; }

        [MaxLength(5000)]
        public string Description { get; set; } = string.Empty; //for company
        
        [MaxLength(5000)]
        public string ContactInfo { get; set; } = string.Empty; //for company
        
        public virtual ICollection<User> Partners { get; set; } = new List<User>();
        
        public virtual ICollection<User> PartnerOf { get; set; } = new List<User>();
        
        public virtual ICollection<Revenue> Revenues { get; set; } = new List<Revenue>();
        
        public virtual ICollection<Tour> Tours { get; set; } = new List<Tour>();
        
        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
        
        public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
    }
}
