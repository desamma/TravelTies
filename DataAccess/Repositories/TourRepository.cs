using DataAccess.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;
using Models.Models;

namespace DataAccess.Repositories;

public class TourRepository  : GenericRepository<Tour>, ITourRepository
{
    private readonly ApplicationDbContext _db;

    public TourRepository(ApplicationDbContext db) : base(db)
    {
        _db = db;
    }
    
    public async Task<List<Tour>> GetTopRatedToursAsync(int count)
    {
        return await _db.Tours
            .Include(t => t.Ratings)
            .Include(t => t.Tickets)
            .OrderByDescending(t => t.Ratings.Any() ? t.Ratings.Average(r => r.Score) : 0)
            .ThenByDescending(t => t.Views)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }
}