using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailAndDemoAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Email", "HashedPassword" },
                values: new object[] { "volunteer@floodlink.lk", "!" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Email", "HashedPassword" },
                values: new object[] { "coordinator@floodlink.lk", "!" });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "HashedPassword", "Name", "Phone", "Role" },
                values: new object[,]
                {
                    { 3, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "depot@floodlink.lk", "!", "Kamal Perera", "+94775550101", "DepotManager" },
                    { 4, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "admin@floodlink.lk", "!", "Admin User", "+94775550100", "Admin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
            // Seed rows use explicit ids, which do not advance the identity sequence; move it past
            // them so the first registered account does not collide with a demo account.
            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('\"Users\"', 'Id'), GREATEST((SELECT MAX(\"Id\") FROM \"Users\"), 1));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "HashedPassword",
                value: "hashed_password_1");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "HashedPassword",
                value: "hashed_password_2");
        }
    }
}
