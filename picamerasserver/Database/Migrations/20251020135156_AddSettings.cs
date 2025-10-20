using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Json = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Type);
                });
            migrationBuilder.InsertData(
                table: "Settings",
                columns: ["Type", "Json"],
                values: new object[,]
                {
                    { "MaxConcurrentNtp", "{\"value\":3}" },
                    { "MaxConcurrentSend", "{\"value\":3}" },
                    { "RequestPictureDelay", "{\"value\":1500}" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
