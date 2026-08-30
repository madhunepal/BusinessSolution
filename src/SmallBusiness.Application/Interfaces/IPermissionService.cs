namespace SmallBusiness.Application.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);
    Task EnsurePermissionAsync(string permission, CancellationToken cancellationToken = default);
}
