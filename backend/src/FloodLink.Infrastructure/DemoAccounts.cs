using FloodLink.Domain.Security;

namespace FloodLink.Infrastructure;

/// <summary>
/// The four demo accounts, one per role. Rows are seeded by the migration with a locked password;
/// the API's startup seeder sets their password hash from <c>Seed:DemoPassword</c>, so no password
/// or hash is ever committed.
/// </summary>
public static class DemoAccounts
{
    public sealed record Account(int Id, string Name, string Email, string Role, string Phone);

    public static readonly IReadOnlyList<Account> All =
    [
        new(1, "John Doe", "volunteer@floodlink.lk", Roles.Volunteer, "+94771234567"),
        new(2, "Jane Smith", "coordinator@floodlink.lk", Roles.Coordinator, "+94779876543"),
        new(3, "Kamal Perera", "depot@floodlink.lk", Roles.DepotManager, "+94775550101"),
        new(4, "Admin User", "admin@floodlink.lk", Roles.Admin, "+94775550100"),
    ];
}
