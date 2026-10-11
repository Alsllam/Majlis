using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Knowledge.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class MeetingMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MeetingId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_MeetingId",
                schema: "knowledge",
                table: "Documents",
                column: "MeetingId",
                unique: true,
                filter: "[MeetingId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_MeetingId",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MeetingId",
                schema: "knowledge",
                table: "Documents");
        }
    }
}
