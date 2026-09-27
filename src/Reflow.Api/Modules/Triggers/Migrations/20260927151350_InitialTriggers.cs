using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.Triggers.Migrations
{
    /// <inheritdoc />
    public partial class InitialTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "triggers");

            migrationBuilder.CreateTable(
                name: "Triggers",
                schema: "triggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Cron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Overlap = table.Column<int>(type: "integer", nullable: false),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SecretTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Triggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                schema: "triggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false),
                    PipelineRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookEvents_Triggers_TriggerId",
                        column: x => x.TriggerId,
                        principalSchema: "triggers",
                        principalTable: "Triggers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Triggers_Kind_IsEnabled_NextRunAt",
                schema: "triggers",
                table: "Triggers",
                columns: new[] { "Kind", "IsEnabled", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Triggers_SecretTokenHash",
                schema: "triggers",
                table: "Triggers",
                column: "SecretTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_TriggerId",
                schema: "triggers",
                table: "WebhookEvents",
                column: "TriggerId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_TriggerId_ExternalEventId",
                schema: "triggers",
                table: "WebhookEvents",
                columns: new[] { "TriggerId", "ExternalEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookEvents",
                schema: "triggers");

            migrationBuilder.DropTable(
                name: "Triggers",
                schema: "triggers");
        }
    }
}
