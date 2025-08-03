using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPictureMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "AnalogueGain",
                table: "CameraPictures",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ColourTemperature",
                table: "CameraPictures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "DigitalGain",
                table: "CameraPictures",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExposureTime",
                table: "CameraPictures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FocusFoM",
                table: "CameraPictures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Lux",
                table: "CameraPictures",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MonotonicTime",
                table: "CameraPictures",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SensorTimestamp",
                table: "CameraPictures",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalogueGain",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "ColourTemperature",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "DigitalGain",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "ExposureTime",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "FocusFoM",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "Lux",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "MonotonicTime",
                table: "CameraPictures");

            migrationBuilder.DropColumn(
                name: "SensorTimestamp",
                table: "CameraPictures");
        }
    }
}
