using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredSalaryRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalaryRange",
                table: "job_profiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsSalaryNegotiable",
                table: "job_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMax",
                table: "job_profiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMin",
                table: "job_profiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSalaryNegotiable",
                table: "job_profiles");

            migrationBuilder.DropColumn(
                name: "SalaryMax",
                table: "job_profiles");

            migrationBuilder.DropColumn(
                name: "SalaryMin",
                table: "job_profiles");

            migrationBuilder.AddColumn<string>(
                name: "SalaryRange",
                table: "job_profiles",
                type: "text",
                nullable: true);
        }
    }
}
