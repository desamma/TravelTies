namespace TravelTies.Areas.Customer.Models;

public class TicketDetailsViewModel
{
    public Guid TicketId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string TourDestination { get; set; } = string.Empty;
    public string? TourPicture { get; set; }
    public DateOnly TourDate { get; set; }
    public int NumberOfSeats { get; set; }
    public decimal TicketPrice { get; set; }
    public bool IsPayed { get; set; }
    public long? PaymentOrderCode { get; set; }
    public DateTime CancellationDateTime { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
}