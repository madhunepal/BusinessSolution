using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Interfaces;

namespace SmallBusiness.Infrastructure.Data;

public sealed class ApplicationDbContextFactory : IApplicationDbContextFactory
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public ApplicationDbContextFactory(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    }
}
