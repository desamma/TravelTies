using System.Linq.Expressions;

namespace DataAccess.Repositories.IRepositories;

public interface IGenericInterface<T> where T : class
{
    IQueryable<T> GetAllQueryable(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true);
    Task<T?> GetAsync(Expression<Func<T, bool>> predicate);
    // Using bool to indicate success or failure of the operation
    Task<bool> AddAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(Guid entityId);
    // Check if an entity exists
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
}