using System.Linq.Expressions;

namespace VetManagement.Application.Contracts.Persistence;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);

    Task<T?> GetByIdAsNoTrackingAsync(int id);

    Task<List<T>> GetAllAsync();

    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    Task AddAsync(T entity);

    void Remove(T entity);

    void Update(T entity);
}
