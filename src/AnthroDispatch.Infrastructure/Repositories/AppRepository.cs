using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Infrastructure.Repositories;

public sealed class AppRepository<T>(AnthroDispatchDbContext context) : IRepository<T>
    where T : class
{
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Set<T>().FindAsync([id], cancellationToken);

    public async Task<List<T>> ListAsync(CancellationToken cancellationToken = default)
        => await context.Set<T>().ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await context.Set<T>().AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await context.Set<T>().AddRangeAsync(entities, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}