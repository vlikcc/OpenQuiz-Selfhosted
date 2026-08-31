using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "Polls",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Polls",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultsShareToken",
                table: "Polls",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartAt",
                table: "Polls",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFiles_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PollCollaborators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollCollaborators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollCollaborators_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollCollaborators_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionBankItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TimeLimit = table.Column<int>(type: "int", nullable: false),
                    QuestionType = table.Column<byte>(type: "tinyint", nullable: false),
                    AllowMultiple = table.Column<bool>(type: "bit", nullable: false),
                    CorrectOptionIndex = table.Column<int>(type: "int", nullable: true),
                    CorrectAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    MaxWords = table.Column<int>(type: "int", nullable: true),
                    WordCloudConfig = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBankItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBankItems_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionBankOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBankOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBankOptions_QuestionBankItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "QuestionBankItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Polls_JoinCode",
                table: "Polls",
                column: "JoinCode",
                unique: true,
                filter: "[JoinCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_ResultsShareToken",
                table: "Polls",
                column: "ResultsShareToken",
                unique: true,
                filter: "[ResultsShareToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_Status_ScheduledStartAt",
                table: "Polls",
                columns: new[] { "Status", "ScheduledStartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_OwnerUserId",
                table: "MediaFiles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PollCollaborators_PollId_UserId",
                table: "PollCollaborators",
                columns: new[] { "PollId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollCollaborators_UserId",
                table: "PollCollaborators",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankItems_OwnerUserId",
                table: "QuestionBankItems",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankOptions_ItemId_OrderIndex",
                table: "QuestionBankOptions",
                columns: new[] { "ItemId", "OrderIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "PollCollaborators");

            migrationBuilder.DropTable(
                name: "QuestionBankOptions");

            migrationBuilder.DropTable(
                name: "QuestionBankItems");

            migrationBuilder.DropIndex(
                name: "IX_Polls_JoinCode",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_ResultsShareToken",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_Status_ScheduledStartAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ResultsShareToken",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ScheduledStartAt",
                table: "Polls");
        }
    }
}
