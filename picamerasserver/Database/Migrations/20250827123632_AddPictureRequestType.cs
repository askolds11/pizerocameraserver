using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPictureRequestType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "PictureRequests",
                newName: "Note");

            migrationBuilder.AddColumn<string>(
                name: "PictureRequestType",
                table: "PictureRequests",
                type: "varchar(50)",
                nullable: false,
                defaultValue: "Other");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PictureRequestType",
                table: "PictureRequests");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "PictureRequests",
                newName: "Type");
        }
    }
}
