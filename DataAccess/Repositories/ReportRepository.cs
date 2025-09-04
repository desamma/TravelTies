using DataAccess.Repositories.IRepositories;
using Models.Models;

namespace DataAccess.Repositories;

public class ReportRepository : GenericRepository<Report>, IReportRepository
{
    public ReportRepository(ApplicationDbContext db) : base(db) { }
}