using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RawPostingsFilter.Persistence;

#nullable disable

namespace RawPostingsFilter.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(RawPostingsDbContext))]
    [Migration("20260521120000_AddSpamOrAdFlag")]
    public partial class AddSpamOrAdFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_spam_or_ad",
                table: "raw_job_postings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_spam_or_ad",
                table: "raw_job_postings");
        }
    }
}
