using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20261004000001_AddAgentExecutionLogErrorMessage")]
    public partial class AddAgentExecutionLogErrorMessage : Migration
    {
        /// <summary>
        /// Adds nullable <c>ErrorMessage</c> column to <c>AgentExecutionLogs</c>.
        /// Stores the human-readable failure reason from <c>AgentResult.ErrorMessage</c>
        /// so failures can be diagnosed without decoding <c>OutputJson</c>.
        /// Column is nullable — null on successful executions.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "AgentExecutionLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "AgentExecutionLogs");
        }
    }
}
