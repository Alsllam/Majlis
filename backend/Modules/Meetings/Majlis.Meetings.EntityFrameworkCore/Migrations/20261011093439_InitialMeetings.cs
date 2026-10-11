using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majlis.Meetings.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "meetings");

            migrationBuilder.CreateTable(
                name: "Meetings",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HeldAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    AttendeesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecordingBlobPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RecordingContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecordingSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    TranscriptText = table.Column<string>(type: "nvarchar(max)", maxLength: 400000, nullable: true),
                    FailureReasonKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceMemberships",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMemberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Minutes",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    DecisionsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    OpenQuestionsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionItemsProposedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Minutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Minutes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "meetings",
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionItems",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItems_Minutes_MinutesId",
                        column: x => x.MinutesId,
                        principalSchema: "meetings",
                        principalTable: "Minutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_ApprovalRequestId",
                schema: "meetings",
                table: "ActionItems",
                column: "ApprovalRequestId",
                unique: true,
                filter: "[ApprovalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_MinutesId",
                schema: "meetings",
                table: "ActionItems",
                column: "MinutesId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_RoomId",
                schema: "meetings",
                table: "Meetings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_TenantId",
                schema: "meetings",
                table: "Meetings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_WorkspaceId_HeldAt",
                schema: "meetings",
                table: "Meetings",
                columns: new[] { "WorkspaceId", "HeldAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_MeetingId",
                schema: "meetings",
                table: "Minutes",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMemberships_TenantId",
                schema: "meetings",
                table: "WorkspaceMemberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMemberships_WorkspaceId_UserId",
                schema: "meetings",
                table: "WorkspaceMemberships",
                columns: new[] { "WorkspaceId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItems",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "WorkspaceMemberships",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "Minutes",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "Meetings",
                schema: "meetings");
        }
    }
}
