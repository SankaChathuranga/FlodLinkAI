using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockMovementsAndConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientReference",
                table: "Reports",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationNote",
                table: "AllocationProposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "AllocationProposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedById",
                table: "AllocationProposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CountedQuantity",
                table: "AllocationProposals",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<double>(type: "double precision", nullable: false),
                    AvailableAfter = table.Column<double>(type: "double precision", nullable: false),
                    ReservedAfter = table.Column<double>(type: "double precision", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockMovements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 1,
                column: "ClientReference",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 2,
                column: "ClientReference",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 3,
                column: "ClientReference",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 4,
                column: "ClientReference",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 5,
                column: "ClientReference",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedBy_ClientReference",
                table: "Reports",
                columns: new[] { "ReportedBy", "ClientReference" },
                unique: true,
                filter: "\"ClientReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationProposals_ConfirmedById",
                table: "AllocationProposals",
                column: "ConfirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryItemId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "InventoryItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Kind",
                table: "StockMovements",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_UserId",
                table: "StockMovements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WorkflowRunId",
                table: "StockMovements",
                column: "WorkflowRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_AllocationProposals_Users_ConfirmedById",
                table: "AllocationProposals",
                column: "ConfirmedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AllocationProposals_Users_ConfirmedById",
                table: "AllocationProposals");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReportedBy_ClientReference",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_AllocationProposals_ConfirmedById",
                table: "AllocationProposals");

            migrationBuilder.DropColumn(
                name: "ClientReference",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ConfirmationNote",
                table: "AllocationProposals");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "AllocationProposals");

            migrationBuilder.DropColumn(
                name: "ConfirmedById",
                table: "AllocationProposals");

            migrationBuilder.DropColumn(
                name: "CountedQuantity",
                table: "AllocationProposals");
        }
    }
}
