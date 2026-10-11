using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Audit.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class ChainVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastBrokenSeq",
                schema: "audit",
                table: "Chains",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LastVerificationValid",
                schema: "audit",
                table: "Chains",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVerifiedAt",
                schema: "audit",
                table: "Chains",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastBrokenSeq",
                schema: "audit",
                table: "Chains");

            migrationBuilder.DropColumn(
                name: "LastVerificationValid",
                schema: "audit",
                table: "Chains");

            migrationBuilder.DropColumn(
                name: "LastVerifiedAt",
                schema: "audit",
                table: "Chains");
        }
    }
}
