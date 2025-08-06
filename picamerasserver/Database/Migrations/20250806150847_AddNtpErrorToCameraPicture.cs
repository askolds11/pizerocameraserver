using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNtpErrorToCameraPicture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "NtpErrorMillis",
                table: "CameraPictures",
                type: "real",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NtpErrorMillis",
                table: "CameraPictures");
        }
    }
}
