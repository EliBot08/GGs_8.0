using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GGs.Server.Migrations
{
    public partial class AddDeviceRegistrations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    Thumbprint = table.Column<string>(type: "TEXT", nullable: false),
                    CommonName = table.Column<string>(type: "TEXT", nullable: true),
                    RegisteredUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_DeviceId",
                table: "DeviceRegistrations",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_IsActive",
                table: "DeviceRegistrations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_LastSeenUtc",
                table: "DeviceRegistrations",
                column: "LastSeenUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceRegistrations");
        }
    }
}

