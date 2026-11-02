using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    public partial class AddCandidateSkills : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_skills",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateProfileId = table.Column<long>(type: "bigint", nullable: false),
                    SkillId = table.Column<long>(type: "bigint", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false, defaultValue: "CV")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_skills_candidate_profiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_skills_CandidateProfileId_SkillId",
                table: "candidate_skills",
                columns: new[] { "CandidateProfileId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_skills_SkillId",
                table: "candidate_skills",
                column: "SkillId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "candidate_skills");
        }
    }
}
