using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Knowledge.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AgentDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "knowledge",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ApprovalRequestId",
                schema: "knowledge",
                table: "Documents",
                column: "ApprovalRequestId",
                unique: true,
                filter: "[ApprovalRequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_ApprovalRequestId",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "knowledge",
                table: "Documents");
        }
    }
}
