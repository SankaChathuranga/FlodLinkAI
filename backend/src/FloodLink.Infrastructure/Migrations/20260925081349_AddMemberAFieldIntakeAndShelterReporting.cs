using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberAFieldIntakeAndShelterReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TriagePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneratedFromReportIds = table.Column<string>(type: "jsonb", nullable: false),
                    PriorityRank = table.Column<int>(type: "integer", nullable: false),
                    PlanSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriagePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriagePlans_WorkflowRuns_CreatedByAgentRunId",
                        column: x => x.CreatedByAgentRunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HashedPassword = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shelters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CurrentOccupancy = table.Column<int>(type: "integer", nullable: false),
                    ContactVolunteerId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shelters_Users_ContactVolunteerId",
                        column: x => x.ContactVolunteerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShelterId = table.Column<int>(type: "integer", nullable: false),
                    ReportedBy = table.Column<int>(type: "integer", nullable: false),
                    NeedType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantityNeeded = table.Column<int>(type: "integer", nullable: false),
                    UrgencyLevel = table.Column<int>(type: "integer", nullable: false),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GpsLat = table.Column<double>(type: "double precision", nullable: true),
                    GpsLng = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reports_Users_ReportedBy",
                        column: x => x.ReportedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "HashedPassword", "Name", "Phone", "Role" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "hashed_password_1", "John Doe", "+94771234567", "Volunteer" },
                    { 2, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "hashed_password_2", "Jane Smith", "+94779876543", "Coordinator" }
                });

            migrationBuilder.InsertData(
                table: "Shelters",
                columns: new[] { "Id", "Capacity", "ContactVolunteerId", "CreatedAt", "CurrentOccupancy", "Latitude", "Longitude", "Name", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 200, 1, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 140, 6.9271000000000003, 79.861199999999997, "Central Colombo Relief Center", "Active", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 150, 1, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 120, 6.9344000000000001, 79.984099999999998, "Kaduwela Community Hall", "Active", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 100, 2, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 45, 7.0839999999999996, 79.992500000000007, "Gampaha Primary School Shelter", "Active", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Reports",
                columns: new[] { "Id", "CreatedAt", "GpsLat", "GpsLng", "NeedType", "PhotoUrl", "QuantityNeeded", "ReportedBy", "ShelterId", "Status", "UrgencyLevel" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 6.9271000000000003, 79.861199999999997, "Water", "https://storage.floodlink.lk/reports/photo_1.jpg", 500, 1, 1, "New", 5 },
                    { 2, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 6.9271000000000003, 79.861199999999997, "Food", "https://storage.floodlink.lk/reports/photo_2.jpg", 300, 1, 1, "Triaged", 4 },
                    { 3, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 6.9344000000000001, 79.984099999999998, "Medical", null, 50, 2, 2, "New", 5 },
                    { 4, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 6.9344000000000001, 79.984099999999998, "Shelter-Repair", "https://storage.floodlink.lk/reports/photo_4.jpg", 20, 1, 2, "InPlan", 3 },
                    { 5, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 7.0839999999999996, 79.992500000000007, "Other", null, 100, 2, 3, "Resolved", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedBy",
                table: "Reports",
                column: "ReportedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ShelterId",
                table: "Reports",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_ContactVolunteerId",
                table: "Shelters",
                column: "ContactVolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_TriagePlans_CreatedByAgentRunId",
                table: "TriagePlans",
                column: "CreatedByAgentRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "TriagePlans");

            migrationBuilder.DropTable(
                name: "Shelters");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
