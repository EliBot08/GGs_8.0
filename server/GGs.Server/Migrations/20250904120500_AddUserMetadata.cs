using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GGs.Server.Migrations
{
    public partial class AddUserMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "AspNetUsers");
        }
    }
}

