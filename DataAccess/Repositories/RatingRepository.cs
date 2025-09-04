using DataAccess.Repositories.IRepositories;
using Models.Models;

namespace DataAccess.Repositories;

public class RatingRepository : GenericRepository<Rating>, IRatingRepository
{
    public RatingRepository(ApplicationDbContext db) : base(db) { }
}