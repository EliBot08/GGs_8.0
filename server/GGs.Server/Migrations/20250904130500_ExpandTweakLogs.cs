using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GGs.Server.Migrations
{
    public partial class ExpandTweakLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistryPath",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistryValueName",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistryValueType",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalValue",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewValue",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionApplied",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScriptApplied",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UndoScript",
                table: "TweakLogs",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RegistryPath", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "RegistryValueName", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "RegistryValueType", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "OriginalValue", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "NewValue", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "ServiceName", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "ActionApplied", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "ScriptApplied", table: "TweakLogs");
            migrationBuilder.DropColumn(name: "UndoScript", table: "TweakLogs");
        }
    }
}

