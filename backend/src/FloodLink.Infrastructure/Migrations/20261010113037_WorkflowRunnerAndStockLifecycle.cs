using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowRunnerAndStockLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dispatches_WorkflowRunId",
                table: "Dispatches");

            migrationBuilder.DropIndex(
                name: "IX_AllocationProposals_WorkflowRunId",
                table: "AllocationProposals");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "WorkflowRuns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "ReportIds",
                table: "WorkflowRuns",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "WorkflowRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RetryOfState",
                table: "WorkflowRuns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepotId",
                table: "Routes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShelterId",
                table: "Routes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowRunId",
                table: "Reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "QuantityReserved",
                table: "InventoryItems",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ReorderThreshold",
                table: "InventoryItems",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            // uuid and integer have no cast between them; the column held no values, so recreate it.
            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "Dispatches");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedById",
                table: "Dispatches",
                type: "integer",
                nullable: true);

            // uuid and integer have no cast between them; the column held no values, so recreate it.
            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "AuditTrail");

            migrationBuilder.AddColumn<int>(
                name: "ActorId",
                table: "AuditTrail",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"AgentExecutionLogs\" ALTER COLUMN \"ToolCallsJson\" TYPE jsonb USING \"ToolCallsJson\"::jsonb;");

            migrationBuilder.AddColumn<bool>(
                name: "IsRetry",
                table: "AgentExecutionLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "QuantityReserved", "ReorderThreshold" },
                values: new object[] { 0.0, 100.0 });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "QuantityReserved", "ReorderThreshold" },
                values: new object[] { 0.0, 50.0 });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "QuantityReserved", "ReorderThreshold" },
                values: new object[] { 0.0, 50.0 });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "QuantityReserved", "ReorderThreshold" },
                values: new object[] { 0.0, 10.0 });

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 1,
                column: "WorkflowRunId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 2,
                column: "WorkflowRunId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 3,
                column: "WorkflowRunId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 4,
                column: "WorkflowRunId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Reports",
                keyColumn: "Id",
                keyValue: 5,
                column: "WorkflowRunId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_CurrentState",
                table: "WorkflowRuns",
                column: "CurrentState");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status",
                table: "Reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_WorkflowRunId",
                table: "Reports",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_ApprovedById",
                table: "Dispatches",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_WorkflowRunId",
                table: "Dispatches",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrail_ActorId",
                table: "AuditTrail",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationProposals_WorkflowRunId_Status",
                table: "AllocationProposals",
                columns: new[] { "WorkflowRunId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrail_Users_ActorId",
                table: "AuditTrail",
                column: "ActorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Dispatches_Users_ApprovedById",
                table: "Dispatches",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_WorkflowRuns_WorkflowRunId",
                table: "Reports",
                column: "WorkflowRunId",
                principalTable: "WorkflowRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrail_Users_ActorId",
                table: "AuditTrail");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispatches_Users_ApprovedById",
                table: "Dispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_WorkflowRuns_WorkflowRunId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowRuns_CurrentState",
                table: "WorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Status",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_WorkflowRunId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Dispatches_ApprovedById",
                table: "Dispatches");

            migrationBuilder.DropIndex(
                name: "IX_Dispatches_WorkflowRunId",
                table: "Dispatches");

            migrationBuilder.DropIndex(
                name: "IX_AuditTrail_ActorId",
                table: "AuditTrail");

            migrationBuilder.DropIndex(
                name: "IX_AllocationProposals_WorkflowRunId_Status",
                table: "AllocationProposals");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ReportIds",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "RetryOfState",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "DepotId",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "ShelterId",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "WorkflowRunId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "QuantityReserved",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ReorderThreshold",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsRetry",
                table: "AgentExecutionLogs");

            // uuid and integer have no cast between them; the column held no values, so recreate it.
            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "Dispatches");

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "Dispatches",
                type: "uuid",
                nullable: true);

            // uuid and integer have no cast between them; the column held no values, so recreate it.
            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "AuditTrail");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "AuditTrail",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"AgentExecutionLogs\" ALTER COLUMN \"ToolCallsJson\" TYPE text USING \"ToolCallsJson\"::text;");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatches_WorkflowRunId",
                table: "Dispatches",
                column: "WorkflowRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllocationProposals_WorkflowRunId",
                table: "AllocationProposals",
                column: "WorkflowRunId");
        }
    }
}
