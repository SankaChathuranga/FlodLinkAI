using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuantityReserved",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReorderThreshold",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "ManagerId",
                table: "Depots",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    AllocationProposalId = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReservedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReservations_AllocationProposals_AllocationProposalId",
                        column: x => x.AllocationProposalId,
                        principalTable: "AllocationProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockReservations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Depots",
                keyColumn: "Id",
                keyValue: 1,
                column: "ManagerId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Depots",
                keyColumn: "Id",
                keyValue: 2,
                column: "ManagerId",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "ReorderThreshold",
                value: 10);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "ReorderThreshold",
                value: 10);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "ReorderThreshold",
                value: 10);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "ReorderThreshold",
                value: 10);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItem_QuantityAvailable_NonNegative",
                table: "InventoryItems",
                sql: "\"QuantityAvailable\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItem_QuantityReserved_Lte_Available",
                table: "InventoryItems",
                sql: "\"QuantityReserved\" <= \"QuantityAvailable\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItem_QuantityReserved_NonNegative",
                table: "InventoryItems",
                sql: "\"QuantityReserved\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Depots_ManagerId",
                table: "Depots",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_AllocationProposalId",
                table: "StockReservations",
                column: "AllocationProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_InventoryItemId",
                table: "StockReservations",
                column: "InventoryItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Depots_Users_ManagerId",
                table: "Depots",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Depots_Users_ManagerId",
                table: "Depots");

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItem_QuantityAvailable_NonNegative",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItem_QuantityReserved_Lte_Available",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItem_QuantityReserved_NonNegative",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Depots_ManagerId",
                table: "Depots");

            migrationBuilder.DropColumn(
                name: "QuantityReserved",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ReorderThreshold",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Depots");
        }
    }
}
