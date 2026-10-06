using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberBInventoryAndMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Depots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Depots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllocationProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepotId = table.Column<int>(type: "integer", nullable: false),
                    ShelterId = table.Column<int>(type: "integer", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationProposals_Depots_DepotId",
                        column: x => x.DepotId,
                        principalTable: "Depots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationProposals_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationProposals_WorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepotId = table.Column<int>(type: "integer", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QuantityAvailable = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Depots_DepotId",
                        column: x => x.DepotId,
                        principalTable: "Depots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Depots",
                columns: new[] { "Id", "CreatedAt", "Latitude", "Longitude", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 6.9271000000000003, 79.861199999999997, "Colombo Central Depot" },
                    { 2, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 6.9344000000000001, 79.984099999999998, "Kaduwela Supply Depot" }
                });

            migrationBuilder.InsertData(
                table: "InventoryItems",
                columns: new[] { "Id", "DepotId", "ItemName", "QuantityAvailable", "Unit", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 1, "Water", 600.0, "bottles", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 1, "Food", 250.0, "packs", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 2, "Water", 400.0, "bottles", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 2, "Medical", 50.0, "kits", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllocationProposals_DepotId",
                table: "AllocationProposals",
                column: "DepotId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationProposals_ShelterId",
                table: "AllocationProposals",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationProposals_WorkflowRunId",
                table: "AllocationProposals",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_DepotId_ItemName",
                table: "InventoryItems",
                columns: new[] { "DepotId", "ItemName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationProposals");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Depots");
        }
    }
}
