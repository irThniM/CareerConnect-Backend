using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_applications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    CandidateProfileId = table.Column<long>(type: "bigint", nullable: false),
                    ResumeId = table.Column<long>(type: "bigint", nullable: true),
                    ResumeCloudinaryPublicIdSnapshot = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ResumeFileNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ResumeContentTypeSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    InvitationId = table.Column<long>(type: "bigint", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CoverLetter = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_applications_candidate_profiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_applications_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_applications_resumes_ResumeId",
                        column: x => x.ResumeId,
                        principalTable: "resumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "application_status_histories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_status_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_status_histories_job_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "job_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_status_histories_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_status_histories_ApplicationId_CreatedAt",
                table: "application_status_histories",
                columns: new[] { "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_application_status_histories_ChangedByUserId",
                table: "application_status_histories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_CandidateProfileId_AppliedAt",
                table: "job_applications",
                columns: new[] { "CandidateProfileId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_job_candidate",
                table: "job_applications",
                columns: new[] { "JobId", "CandidateProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_ResumeId",
                table: "job_applications",
                column: "ResumeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_status_histories");

            migrationBuilder.DropTable(
                name: "job_applications");
        }
    }
}
