namespace SmallBusiness.Application.Interfaces;

public interface IApplicationDbContextFactory
{
    Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}
