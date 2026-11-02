using CareerConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261102000000_SeedDefaultCompanyPackages")]
    public partial class SeedDefaultCompanyPackages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO packages ("Name", "Description", "Price", "DurationDays", "MemberLimit", "JobPostLimit", "IsActive", "CreatedAt")
                SELECT seed."Name", seed."Description", seed."Price", seed."DurationDays", seed."MemberLimit", seed."JobPostLimit", TRUE, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('Khởi động', 'Phù hợp doanh nghiệp nhỏ, đăng tối đa 5 tin trong 30 ngày.', 49000::numeric, 30, 1, 5),
                    ('Tăng tốc', 'Tăng khả năng tiếp cận ứng viên với 20 tin tuyển dụng trong 30 ngày.', 149000::numeric, 30, 3, 20),
                    ('Chuyên nghiệp', 'Dành cho đội ngũ tuyển dụng thường xuyên, tối đa 100 tin trong 90 ngày.', 399000::numeric, 90, 10, 100)
                ) AS seed("Name", "Description", "Price", "DurationDays", "MemberLimit", "JobPostLimit")
                WHERE NOT EXISTS (
                    SELECT 1 FROM packages existing
                    WHERE LOWER(existing."Name") = LOWER(seed."Name")
                );
                """);
        }

        // Keep seeded packages if this data migration is rolled back: they may already have purchases.
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}