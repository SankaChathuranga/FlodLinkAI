using FloodLink.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace FloodLink.Api.Auth;

/// <summary>
/// Authorization policy names, one per row group of the access matrix in
/// <c>FloodLink_Project_Finalization.md</c>. Every policy also admits <see cref="Roles.Admin"/>.
/// Endpoints that combine a role with ownership (a Volunteer's own report or run) use plain
/// <c>[Authorize]</c> and check in the action with <see cref="AccessExtensions"/>.
/// </summary>
public static class Policies
{
    public const string Volunteer = "Volunteer";
    public const string Coordinator = "Coordinator";
    public const string DepotManager = "DepotManager";
    /// <summary>Read access to depots, inventory and allocations.</summary>
    public const string StockReader = "StockReader";
    public const string Admin = "Admin";

    private static readonly Dictionary<string, string[]> RolesByPolicy = new()
    {
        [Volunteer] = [Roles.Volunteer],
        [Coordinator] = [Roles.Coordinator],
        [DepotManager] = [Roles.DepotManager],
        [StockReader] = [Roles.Coordinator, Roles.DepotManager],
        [Admin] = [],
    };

    public static void AddFloodLinkPolicies(this AuthorizationOptions options)
    {
        foreach (var (name, roles) in RolesByPolicy)
        {
            options.AddPolicy(name, p => p.RequireAuthenticatedUser().AddRequirements(new RoleOrAdminRequirement(roles)));
        }
    }
}

/// <summary>Satisfied when the user holds one of <see cref="AllowedRoles"/> or is an Admin.</summary>
public sealed class RoleOrAdminRequirement(IReadOnlyCollection<string> allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles;
}

public sealed class RoleOrAdminHandler : AuthorizationHandler<RoleOrAdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleOrAdminRequirement requirement)
    {
        if (context.User.IsInAnyRoleOrAdmin(requirement.AllowedRoles.ToArray()))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
