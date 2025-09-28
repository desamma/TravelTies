using Models.Models;

namespace TravelTies.Areas.Customer.Models;

public class TourDetailViewModel
{
    public Tour Tour { get; set; } = null!;
    public IEnumerable<Rating> Ratings { get; set; } = Array.Empty<Rating>();
    public double AverageRating { get; set; }
    public int ReviewsCount { get; set; }
    public int RemainingTickets { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool CanRate { get; set; } // user bought ticket -> can rate
    public Guid TourId => Tour.TourId;
}