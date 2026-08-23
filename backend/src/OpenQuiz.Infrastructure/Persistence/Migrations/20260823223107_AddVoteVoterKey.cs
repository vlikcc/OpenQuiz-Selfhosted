using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoteVoterKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Votes_QuestionId_UserId",
                table: "Votes");

            migrationBuilder.AddColumn<string>(
                name: "VoterKey",
                table: "Votes",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE Votes
                SET VoterKey = CASE
                    WHEN UserId IS NULL THEN 'anon:' + LOWER(LTRIM(RTRIM(UserName)))
                    ELSE 'user:' + LOWER(CONVERT(nvarchar(36), UserId))
                END;
                """);

            // Anonymous voters were never deduplicated, so existing data can
            // hold repeat votes that the new unique index would reject. Keep the
            // first vote each voter cast on a question and drop the rest.
            // Scores are not recomputed; re-run a session to get clean totals.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT Id, ROW_NUMBER() OVER (
                        PARTITION BY QuestionId, VoterKey ORDER BY CreatedAt, Id) AS rn
                    FROM Votes
                )
                DELETE FROM Votes WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_QuestionId_VoterKey",
                table: "Votes",
                columns: new[] { "QuestionId", "VoterKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Votes_QuestionId_VoterKey",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "VoterKey",
                table: "Votes");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_QuestionId_UserId",
                table: "Votes",
                columns: new[] { "QuestionId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }
    }
}
