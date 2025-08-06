using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPictureTimesAndMoreMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "AeState",
                table: "CameraPictures",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FrameDuration",
                table: "CameraPictures",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PictureRequestReceived",
                table: "CameraPictures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Requested",
                table: "CameraPictures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WaitTimeNanos",
                table: "CameraPictures",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AeState",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "FrameDuration",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "PictureRequestReceived",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "Requested",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "WaitTimeNanos",
                table: "CameraPictures");
        }
    }
}
