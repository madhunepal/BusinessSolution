using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.IntegrationTests;

internal sealed class SqlServerApplicationDbContextFactory : IApplicationDbContextFactory
{
    private readonly SqlServerTestFixture _fixture;
    private readonly ITenantContext _tenantContext;

    public SqlServerApplicationDbContextFactory(SqlServerTestFixture fixture, ITenantContext tenantContext)
    {
        _fixture = fixture;
        _tenantContext = tenantContext;
    }

    public Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IApplicationDbContext>(_fixture.CreateContext(_tenantContext));
    }
}
