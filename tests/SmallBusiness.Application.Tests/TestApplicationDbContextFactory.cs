using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.Application.Tests;

internal sealed class TestApplicationDbContextFactory : IApplicationDbContextFactory
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly ITenantContext _tenantContext;

    public TestApplicationDbContextFactory(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
    {
        _options = options;
        _tenantContext = tenantContext;
    }

    private int _createdContextCount;

    public int CreatedContextCount => _createdContextCount;

    public Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _createdContextCount);
        return Task.FromResult<IApplicationDbContext>(new ApplicationDbContext(_options, _tenantContext));
    }
}
