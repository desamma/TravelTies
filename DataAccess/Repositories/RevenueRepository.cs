using DataAccess.Repositories.IRepositories;
using Models.Models;

namespace DataAccess.Repositories;

public class RevenueRepository : GenericRepository<Revenue>, IRevenueRepository
{
    public RevenueRepository(ApplicationDbContext db) : base(db) { }
}