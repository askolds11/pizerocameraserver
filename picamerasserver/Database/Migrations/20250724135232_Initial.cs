using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cameras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PictureRequests",
                columns: table => new
                {
                    Uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PictureTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PictureRequests", x => x.Uuid);
                });

            migrationBuilder.CreateTable(
                name: "CameraPictures",
                columns: table => new
                {
                    CameraId = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    PictureRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraPictureStatus = table.Column<string>(type: "varchar(20)", nullable: true),
                    StatusMessage = table.Column<string>(type: "text", nullable: true),
                    ReceivedTaken = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedSaved = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedSent = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PictureTaken = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraPictures", x => new { x.CameraId, x.PictureRequestId });
                    table.ForeignKey(
                        name: "FK_CameraPictures_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CameraPictures_PictureRequests_PictureRequestId",
                        column: x => x.PictureRequestId,
                        principalTable: "PictureRequests",
                        principalColumn: "Uuid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraPictures_PictureRequestId",
                table: "CameraPictures",
                column: "PictureRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraPictures");

            migrationBuilder.DropTable(
                name: "Cameras");

            migrationBuilder.DropTable(
                name: "PictureRequests");
        }
    }
}
