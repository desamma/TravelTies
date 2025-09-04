using DataAccess.Repositories.IRepositories;
using Models.Models;

namespace DataAccess.Repositories;

public class TourRepository  : GenericRepository<Tour>, ITourRepository
{
    public TourRepository(ApplicationDbContext db) : base(db) { }
}