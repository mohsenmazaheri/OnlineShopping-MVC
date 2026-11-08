using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OnlineShop.Application.Authorization;

namespace OnlineShop.Infrastructure.Identity;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Grants a permission when any of the user's role claims maps to it (fixed map in <see cref="RolePermissions"/>).</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        if (RolePermissions.For(roles).Contains(requirement.Permission))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Creates "permission:xyz" policies on demand, so endpoints can write
/// <c>.RequireAuthorization(Permissions.Policy(Permissions.CatalogManage))</c> without registering each policy.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (!policyName.StartsWith(Permissions.PolicyPrefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var permission = policyName[Permissions.PolicyPrefix.Length..];
        if (!Permissions.All.Contains(permission))
            throw new InvalidOperationException($"Unknown permission '{permission}' in policy '{policyName}'.");

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}
