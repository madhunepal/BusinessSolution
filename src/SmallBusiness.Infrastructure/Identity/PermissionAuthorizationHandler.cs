using Microsoft.AspNetCore.Authorization;
using SmallBusiness.Application.Interfaces;

namespace SmallBusiness.Infrastructure.Identity;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (await _permissionService.HasPermissionAsync(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
