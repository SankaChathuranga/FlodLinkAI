using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: true),
                    OutputJson = table.Column<string>(type: "text", nullable: true),
                    ToolCallsJson = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentExecutionLogs_WorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutionLogs_WorkflowRunId",
                table: "AgentExecutionLogs",
                column: "WorkflowRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Routes");
            migrationBuilder.DropTable(name: "AgentExecutionLogs");
            migrationBuilder.DropTable(name: "WorkflowRuns");
        }
    }
}
