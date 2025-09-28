namespace TravelTies.Areas.Customer.Models;

public class FeaturedTourViewModel
{
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public double AvgRating { get; set; }
    public int ReviewsCount { get; set; }
    public DateOnly NextDeparture { get; set; }
    public int AvailableSlots { get; set; }
}
