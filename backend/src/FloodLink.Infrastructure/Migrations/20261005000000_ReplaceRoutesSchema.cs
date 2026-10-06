using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20261005000000_ReplaceRoutesSchema")]
    public partial class ReplaceRoutesSchema : Migration
    {
        /// <summary>
        /// Drops the old Routes table (which had coordinate columns, DistanceKm, EtaMinutes,
        /// AllocationProposalId) and recreates it with the fixed Phase 5 schema:
        /// WorkflowRunId (FK → WorkflowRuns), DistanceMeters (integer),
        /// EstimatedDurationSeconds (integer), PolylineString (text).
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Routes");

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    PolylineString = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Routes_WorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_WorkflowRunId",
                table: "Routes",
                column: "WorkflowRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Routes");

            // Restore original schema (no FK, coordinate columns present)
            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationProposalId = table.Column<int>(type: "integer", nullable: false),
                    OriginLat = table.Column<double>(type: "double precision", nullable: false),
                    OriginLng = table.Column<double>(type: "double precision", nullable: false),
                    DestLat = table.Column<double>(type: "double precision", nullable: false),
                    DestLng = table.Column<double>(type: "double precision", nullable: false),
                    DistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    EtaMinutes = table.Column<double>(type: "double precision", nullable: false),
                    RoutePolyline = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });
        }
    }
}
