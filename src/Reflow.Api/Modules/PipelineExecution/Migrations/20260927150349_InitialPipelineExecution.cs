using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.PipelineExecution.Migrations
{
    /// <inheritdoc />
    public partial class InitialPipelineExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pipeline_execution");

            migrationBuilder.CreateTable(
                name: "ExecutionLogs",
                schema: "pipeline_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRuns",
                schema: "pipeline_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EdgesJson = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskRuns",
                schema: "pipeline_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConfigJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    OutputJson = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskRuns_PipelineRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "pipeline_execution",
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskAttempts",
                schema: "pipeline_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAttempts_TaskRuns_TaskRunId",
                        column: x => x.TaskRunId,
                        principalSchema: "pipeline_execution",
                        principalTable: "TaskRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLogs_RunId",
                schema: "pipeline_execution",
                table: "ExecutionLogs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_PipelineId",
                schema: "pipeline_execution",
                table: "PipelineRuns",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_Status",
                schema: "pipeline_execution",
                table: "PipelineRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttempts_TaskRunId",
                schema: "pipeline_execution",
                table: "TaskAttempts",
                column: "TaskRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRuns_NotBefore",
                schema: "pipeline_execution",
                table: "TaskRuns",
                column: "NotBefore");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRuns_RunId",
                schema: "pipeline_execution",
                table: "TaskRuns",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRuns_RunId_Status",
                schema: "pipeline_execution",
                table: "TaskRuns",
                columns: new[] { "RunId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionLogs",
                schema: "pipeline_execution");

            migrationBuilder.DropTable(
                name: "TaskAttempts",
                schema: "pipeline_execution");

            migrationBuilder.DropTable(
                name: "TaskRuns",
                schema: "pipeline_execution");

            migrationBuilder.DropTable(
                name: "PipelineRuns",
                schema: "pipeline_execution");
        }
    }
}
