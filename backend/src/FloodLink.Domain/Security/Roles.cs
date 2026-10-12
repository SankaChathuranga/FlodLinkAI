namespace FloodLink.Domain.Security;

/// <summary>
/// The single source of role names used in tokens, the Users table and every authorization check.
/// Admin is implicitly allowed by every role-based policy (see <c>RoleOrAdminHandler</c> in the API).
/// </summary>
public static class Roles
{
    public const string Volunteer = "Volunteer";
    public const string Coordinator = "Coordinator";
    public const string DepotManager = "DepotManager";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Volunteer, Coordinator, DepotManager, Admin];

    /// <summary>Roles an Admin may create through <c>POST /api/users</c>.</summary>
    public static readonly IReadOnlyList<string> StaffCreatable = [Coordinator, DepotManager];
}
