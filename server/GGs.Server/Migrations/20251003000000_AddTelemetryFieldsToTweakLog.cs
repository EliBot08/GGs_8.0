using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GGs.Server.Migrations
{
    /// <summary>
    /// Migration to add enhanced telemetry fields to TweakApplicationLog.
    /// Implements Prompt 4: OperationId, CorrelationId, synchronized timestamps, reason codes, and policy decisions.
    /// </summary>
    public partial class AddTelemetryFieldsToTweakLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add OperationId for distributed tracing
            migrationBuilder.AddColumn<string>(
                name: "OperationId",
                table: "TweakLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Add CorrelationId for linking related operations
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            // Add InitiatedUtc for synchronized timestamp tracking
            migrationBuilder.AddColumn<DateTime>(
                name: "InitiatedUtc",
                table: "TweakLogs",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "datetime('now')");

            // Add CompletedUtc for operation completion tracking
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedUtc",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            // Add ReasonCode for standardized error/policy codes
            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            // Add PolicyDecision for audit trail
            migrationBuilder.AddColumn<string>(
                name: "PolicyDecision",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            // Create indexes for efficient querying
            migrationBuilder.CreateIndex(
                name: "IX_TweakLogs_OperationId",
                table: "TweakLogs",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_TweakLogs_CorrelationId",
                table: "TweakLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TweakLogs_InitiatedUtc",
                table: "TweakLogs",
                column: "InitiatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TweakLogs_ReasonCode",
                table: "TweakLogs",
                column: "ReasonCode");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_TweakLogs_OperationId",
                table: "TweakLogs");

            migrationBuilder.DropIndex(
                name: "IX_TweakLogs_CorrelationId",
                table: "TweakLogs");

            migrationBuilder.DropIndex(
                name: "IX_TweakLogs_InitiatedUtc",
                table: "TweakLogs");

            migrationBuilder.DropIndex(
                name: "IX_TweakLogs_ReasonCode",
                table: "TweakLogs");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "TweakLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "TweakLogs");

            migrationBuilder.DropColumn(
                name: "InitiatedUtc",
                table: "TweakLogs");

            migrationBuilder.DropColumn(
                name: "CompletedUtc",
                table: "TweakLogs");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "TweakLogs");

            migrationBuilder.DropColumn(
                name: "PolicyDecision",
                table: "TweakLogs");
        }
    }
}

