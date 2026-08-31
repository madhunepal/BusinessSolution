using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.Web.Tests;

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

    public Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IApplicationDbContext>(new ApplicationDbContext(_options, _tenantContext));
    }
}
