using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    MemberLimit = table.Column<int>(type: "integer", nullable: true),
                    JobPostLimit = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "company_subscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemainingJobPosts = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_subscriptions_companies_profile_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies_profile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_subscriptions_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    SubscriptionId = table.Column<long>(type: "bigint", nullable: true),
                    Provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TransactionRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ProviderTransactionNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponseCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RawReturnDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_companies_profile_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies_profile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_transactions_company_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "company_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_subscriptions_CompanyId_Status_EndAt",
                table: "company_subscriptions",
                columns: new[] { "CompanyId", "Status", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_company_subscriptions_PackageId",
                table: "company_subscriptions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_packages_IsActive",
                table: "packages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_CompanyId_CreatedAt",
                table: "payment_transactions",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_SubscriptionId",
                table: "payment_transactions",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_TransactionRef",
                table: "payment_transactions",
                column: "TransactionRef",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "company_subscriptions");

            migrationBuilder.DropTable(
                name: "packages");
        }
    }
}
