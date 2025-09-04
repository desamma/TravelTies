using System.Linq.Expressions;
using DataAccess.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class GenericRepository<T> : IGenericInterface<T> where T : class
{
    private readonly ApplicationDbContext _db;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(ApplicationDbContext db)
    {
        _db = db;
        _dbSet = _db.Set<T>(); // Works for any entity type
    }

    public IQueryable<T> GetAllQueryable(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
            query = query.AsNoTracking();

        if (predicate != null)
            query = query.Where(predicate);

        return query;
    }

    public async Task<T?> GetAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate);
    }

    public async Task<bool> AddAsync(T entity)
    {
        _dbSet.Add(entity);
        return await _db.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return await _db.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(Guid entityId)
    {
        var entity = await _dbSet.FindAsync(entityId);
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        return await _db.SaveChangesAsync() > 0;
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }
}
