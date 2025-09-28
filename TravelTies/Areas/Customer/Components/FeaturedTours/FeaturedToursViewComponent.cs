using DataAccess;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Mvc;
using TravelTies.Areas.Customer.Models;

namespace TravelTies.Areas.Customer.Components.FeaturedTours;

public class FeaturedToursViewComponent : ViewComponent
{
    private readonly ITourRepository _tourRepository;

    public FeaturedToursViewComponent(ITourRepository tourRepository)
    {
        _tourRepository = tourRepository;
    }

    public async Task<IViewComponentResult> InvokeAsync(int count = 4)
    {
        var tours = await _tourRepository.GetTopRatedToursAsync(count);

        // Calculate additional info for each tour
        var model = tours.Select(t => new FeaturedTourViewModel
        {
            TourId = t.TourId,
            TourName = t.TourName,
            Destination = t.Destination,
            Picture = t.Picture,
            Price = t.Discount != 0 ? (t.Price * t.Discount / 100) :  t.Price,
            OriginalPrice = t.Price, // if you store discount %
            AvgRating = t.Ratings.Any() ? t.Ratings.Average(r => r.Score) : 0,
            ReviewsCount = t.Ratings.Count,
            NextDeparture = t.TourStartDate,
            AvailableSlots = t.NumberOfPassenger - t.Tickets.Count
        }).ToList();

        return View(model);
    }

}