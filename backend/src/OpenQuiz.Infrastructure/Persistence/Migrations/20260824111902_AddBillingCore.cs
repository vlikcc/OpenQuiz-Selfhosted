using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlanCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    CurrentPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ProviderCustomerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderSubscriptionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingAccounts_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntitlementOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntitlementOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntitlementOverrides_BillingAccounts_BillingAccountId",
                        column: x => x.BillingAccountId,
                        principalTable: "BillingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageCounters_BillingAccounts_BillingAccountId",
                        column: x => x.BillingAccountId,
                        principalTable: "BillingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Polls_CreatorId_Status",
                table: "Polls",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_OrganizationId",
                table: "BillingAccounts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_OwnerUserId",
                table: "BillingAccounts",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_ProviderSubscriptionId",
                table: "BillingAccounts",
                column: "ProviderSubscriptionId",
                unique: true,
                filter: "[ProviderSubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EntitlementOverrides_BillingAccountId_Key",
                table: "EntitlementOverrides",
                columns: new[] { "BillingAccountId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageCounters_BillingAccountId_Metric_PeriodKey",
                table: "UsageCounters",
                columns: new[] { "BillingAccountId", "Metric", "PeriodKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntitlementOverrides");

            migrationBuilder.DropTable(
                name: "UsageCounters");

            migrationBuilder.DropTable(
                name: "BillingAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Polls_CreatorId_Status",
                table: "Polls");
        }
    }
}
