using DataAccess.Repositories.IRepositories;
using Models.Models;

namespace DataAccess.Repositories;

public class TicketRepository :  GenericRepository<Ticket>, ITicketRepository
{
    public TicketRepository(ApplicationDbContext db) : base(db) { }
}