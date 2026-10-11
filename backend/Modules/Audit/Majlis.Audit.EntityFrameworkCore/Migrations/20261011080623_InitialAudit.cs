using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Audit.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "Chains",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSeq = table.Column<long>(type: "bigint", nullable: false),
                    LastHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chains_TenantId",
                schema: "audit",
                table: "Chains",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entries_SourceKey",
                schema: "audit",
                table: "Entries",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entries_TenantId",
                schema: "audit",
                table: "Entries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_TenantId_OccurredAt",
                schema: "audit",
                table: "Entries",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_TenantId_Seq",
                schema: "audit",
                table: "Entries",
                columns: new[] { "TenantId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entries_WorkspaceId_OccurredAt",
                schema: "audit",
                table: "Entries",
                columns: new[] { "WorkspaceId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chains",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "Entries",
                schema: "audit");
        }
    }
}
