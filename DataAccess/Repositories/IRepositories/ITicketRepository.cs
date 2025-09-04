using Models.Models;

namespace DataAccess.Repositories.IRepositories;

public interface ITicketRepository : IGenericInterface<Ticket>
{
    //Todo:Task<IEnumerable<Ticket>> SearchAsync(string searchTerm);
}