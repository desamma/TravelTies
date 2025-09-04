using Models.Models;

namespace DataAccess.Repositories.IRepositories;

public interface ITourRepository : IGenericInterface<Tour>
{
    //Todo:Task<IEnumerable<Tour>> SearchAsync(string searchTerm);
}