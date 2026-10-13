using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    public partial class ReplaceFreePostQuotaWithCredits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JobPostCreditsRemaining",
                table: "companies_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE companies_profile
                SET "JobPostCreditsRemaining" = GREATEST(0, 2 - "FreeJobPostsUsed");
                """);

            migrationBuilder.DropColumn(
                name: "FreeJobPostsUsed",
                table: "companies_profile");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeJobPostsUsed",
                table: "companies_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE companies_profile
                SET "FreeJobPostsUsed" = GREATEST(0, 2 - LEAST("JobPostCreditsRemaining", 2));
                """);

            migrationBuilder.DropColumn(
                name: "JobPostCreditsRemaining",
                table: "companies_profile");
        }
    }
}
