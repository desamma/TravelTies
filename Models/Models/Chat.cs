using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Models.Models;

public class Chat
{
    public Guid ChatId { get; set; }

    public string Message { get; set; } = string.Empty;

    [DataType(DataType.DateTime)]
    [DisplayFormat(DataFormatString = "{HH:mm dd/MM/yyyy}")]
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    public bool IsUserChat { get; set; } = true;

    public Guid? SenderId { get; set; }
    [ValidateNever]
    public virtual User Sender { get; set; }

    public Guid? ReceiverId { get; set; }
    [ValidateNever]
    public virtual User Receiver { get; set; }
}