using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CareerConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProfileEmbeddingVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the old text column intact in case it contains legacy data, and create
            // a separate pgvector column for semantic matching.
            migrationBuilder.RenameColumn(
                name: "Embedding",
                table: "candidate_profiles",
                newName: "EmbeddingLegacy");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "candidate_profiles",
                type: "vector(3072)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "candidate_profiles");

            migrationBuilder.RenameColumn(
                name: "EmbeddingLegacy",
                table: "candidate_profiles",
                newName: "Embedding");
        }
    }
}
