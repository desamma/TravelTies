namespace TravelTies.Areas.Customer.Models;

public class TicketViewModel
{
    public Guid TicketId { get; set; }
    public Guid? TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string? PictureFirstUrl { get; set; }    // first image from Tour.Picture (comma separated)
    public DateOnly TourDate { get; set; }
    public int NumberOfSeats { get; set; }
    public decimal TicketPrice { get; set; }        // total for this ticket (all seats)
    public decimal PricePerPerson => NumberOfSeats > 0 ? TicketPrice / NumberOfSeats : TicketPrice;
    public string? Destination { get; set; }
    public bool IsPayed { get; set; }
}