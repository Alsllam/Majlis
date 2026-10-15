using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Workspaces.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class GroupGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ViaGroupId",
                schema: "workspaces",
                table: "Members",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupGrants",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupGrants_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "workspaces",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupMemberships",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMemberships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_ViaGroupId",
                schema: "workspaces",
                table: "Members",
                column: "ViaGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupGrants_GroupId",
                schema: "workspaces",
                table: "GroupGrants",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupGrants_WorkspaceId_GroupId",
                schema: "workspaces",
                table: "GroupGrants",
                columns: new[] { "WorkspaceId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_GroupId_UserId",
                schema: "workspaces",
                table: "GroupMemberships",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_TenantId",
                schema: "workspaces",
                table: "GroupMemberships",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupGrants",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "GroupMemberships",
                schema: "workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Members_ViaGroupId",
                schema: "workspaces",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ViaGroupId",
                schema: "workspaces",
                table: "Members");
        }
    }
}
