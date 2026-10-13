using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    public partial class AddCompanyFreeJobPostQuota : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT normalized_tax_code
                        FROM (
                            SELECT NULLIF(regexp_replace("TaxCode", '[[:space:].-]', '', 'g'), '') AS normalized_tax_code
                            FROM companies_profile
                            WHERE "TaxCode" IS NOT NULL
                        ) AS normalized_codes
                        WHERE normalized_tax_code IS NOT NULL
                        GROUP BY normalized_tax_code
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Có nhiều công ty đang dùng cùng mã số thuế sau khi chuẩn hóa. Hãy xử lý các mã trùng trước khi chạy migration.';
                    END IF;
                END $$;

                UPDATE companies_profile
                SET "TaxCode" = NULLIF(regexp_replace("TaxCode", '[[:space:].-]', '', 'g'), '')
                WHERE "TaxCode" IS NOT NULL;
                """);

            migrationBuilder.AddColumn<int>(
                name: "FreeJobPostsUsed",
                table: "companies_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeJobPostsUsed",
                table: "companies_profile");
        }
    }
}
