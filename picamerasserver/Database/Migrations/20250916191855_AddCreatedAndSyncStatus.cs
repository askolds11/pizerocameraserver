using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace picamerasserver.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAndSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add nullable
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Created",
                table: "PictureSets",
                type: "timestamp with time zone",
                nullable: true);
            
            // Set values
            migrationBuilder.Sql(@"
            UPDATE ""PictureSets""
            SET ""Created"" =
                to_timestamp(
                    (
                        ('x' || 
                        lpad(
                            substring(encode(uuid_send(""Uuid""), 'hex') for 12), 
                            16, 
                            '0')
                        )::bit(64)::bigint
                    ) / 1000.0
                );
            ");
            
            // Set non nullable
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Created",
                table: "PictureSets",
                type: "timestamp with time zone",
                nullable: false );
            
            migrationBuilder.AddColumn<bool>(
                name: "Synced",
                table: "CameraPictures",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Created",
                table: "PictureSets");

            migrationBuilder.DropColumn(
                name: "Synced",
                table: "CameraPictures");
        }
    }
}
