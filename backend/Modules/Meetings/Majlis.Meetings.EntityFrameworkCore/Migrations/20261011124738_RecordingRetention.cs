using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Meetings.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RecordingRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TranscribedAt",
                schema: "meetings",
                table: "Meetings",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranscribedAt",
                schema: "meetings",
                table: "Meetings");
        }
    }
}
