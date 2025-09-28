namespace TravelTies.Areas.Customer.Models;

public class TicketListItemViewModel
{
    public Guid TicketId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateOnly TourDate { get; set; }
    public int NumberOfSeats { get; set; }
    public decimal TicketPrice { get; set; }
    public bool IsPayed { get; set; }
    public long? PaymentOrderCode { get; set; }
    public string? TourPicture { get; set; }
}