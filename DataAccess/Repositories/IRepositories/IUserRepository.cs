using Models.Models;

namespace DataAccess.Repositories.IRepositories;

public interface IUserRepository  : IGenericInterface<User>
{
    //Todo:Task<IEnumerable<User>> SearchAsync(string searchTerm);
}