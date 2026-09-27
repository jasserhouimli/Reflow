using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.Pipelines.Migrations
{
    /// <inheritdoc />
    public partial class InitialPipelines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pipelines");

            migrationBuilder.CreateTable(
                name: "Pipelines",
                schema: "pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineVersions",
                schema: "pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineEdges",
                schema: "pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineEdges_Pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalSchema: "pipelines",
                        principalTable: "Pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineNodes",
                schema: "pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConfigJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Label = table.Column<string>(type: "text", nullable: true),
                    PositionX = table.Column<double>(type: "double precision", nullable: false),
                    PositionY = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineNodes_Pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalSchema: "pipelines",
                        principalTable: "Pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineEdges_PipelineId",
                schema: "pipelines",
                table: "PipelineEdges",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineNodes_PipelineId",
                schema: "pipelines",
                table: "PipelineNodes",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_OwnerId",
                schema: "pipelines",
                table: "Pipelines",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineVersions_PipelineId_VersionNumber",
                schema: "pipelines",
                table: "PipelineVersions",
                columns: new[] { "PipelineId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineEdges",
                schema: "pipelines");

            migrationBuilder.DropTable(
                name: "PipelineNodes",
                schema: "pipelines");

            migrationBuilder.DropTable(
                name: "PipelineVersions",
                schema: "pipelines");

            migrationBuilder.DropTable(
                name: "Pipelines",
                schema: "pipelines");
        }
    }
}
