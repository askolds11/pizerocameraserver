using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPictureSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PictureRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PictureSetId",
                table: "PictureRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "PictureRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PictureSets",
                columns: table => new
                {
                    Uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PictureSets", x => x.Uuid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PictureRequests_PictureSetId",
                table: "PictureRequests",
                column: "PictureSetId");

            migrationBuilder.AddForeignKey(
                name: "FK_PictureRequests_PictureSets_PictureSetId",
                table: "PictureRequests",
                column: "PictureSetId",
                principalTable: "PictureSets",
                principalColumn: "Uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PictureRequests_PictureSets_PictureSetId",
                table: "PictureRequests");

            migrationBuilder.DropTable(
                name: "PictureSets");

            migrationBuilder.DropIndex(
                name: "IX_PictureRequests_PictureSetId",
                table: "PictureRequests");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PictureRequests");

            migrationBuilder.DropColumn(
                name: "PictureSetId",
                table: "PictureRequests");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "PictureRequests");
        }
    }
}
