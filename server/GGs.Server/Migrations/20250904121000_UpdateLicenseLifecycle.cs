using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GGs.Server.Migrations
{
    public partial class UpdateLicenseLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedDevicesJson",
                table: "Licenses",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "DeveloperMode",
                table: "Licenses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxDevices",
                table: "Licenses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Licenses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Licenses",
                type: "TEXT",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<int>(
                name: "UsageCount",
                table: "Licenses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AssignedDevicesJson", table: "Licenses");
            migrationBuilder.DropColumn(name: "DeveloperMode", table: "Licenses");
            migrationBuilder.DropColumn(name: "MaxDevices", table: "Licenses");
            migrationBuilder.DropColumn(name: "Notes", table: "Licenses");
            migrationBuilder.DropColumn(name: "Status", table: "Licenses");
            migrationBuilder.DropColumn(name: "UsageCount", table: "Licenses");
        }
    }
}

