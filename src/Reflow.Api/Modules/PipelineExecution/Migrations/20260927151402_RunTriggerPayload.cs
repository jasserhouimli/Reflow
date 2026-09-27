using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.PipelineExecution.Migrations
{
    /// <inheritdoc />
    public partial class RunTriggerPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerPayloadJson",
                schema: "pipeline_execution",
                table: "PipelineRuns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerPayloadJson",
                schema: "pipeline_execution",
                table: "PipelineRuns");
        }
    }
}
