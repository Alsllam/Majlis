using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Notifications.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class DailyDigest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDigestAt",
                schema: "notifications",
                table: "Settings",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDigestAt",
                schema: "notifications",
                table: "Settings");
        }
    }
}
