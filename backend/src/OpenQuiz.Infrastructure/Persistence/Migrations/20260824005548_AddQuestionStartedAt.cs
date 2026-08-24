using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "QuestionStartedAt",
                table: "Polls",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestionStartedAt",
                table: "Polls");
        }
    }
}
