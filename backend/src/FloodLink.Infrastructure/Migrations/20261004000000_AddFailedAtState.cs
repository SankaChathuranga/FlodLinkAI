using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20261004000000_AddFailedAtState")]
    public partial class AddFailedAtState : Migration
    {
        /// <summary>
        /// Adds the nullable <c>FailedAtState</c> column to <c>WorkflowRuns</c>.
        /// Stores which pipeline stage was active when a run transitioned to Failed,
        /// enabling single-query failure diagnosis without scanning AgentExecutionLogs.
        /// Column is nullable: has no meaning for runs that have not failed.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailedAtState",
                table: "WorkflowRuns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedAtState",
                table: "WorkflowRuns");
        }
    }
}
