using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "thumbnail_path",
                table: "submission_files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "payload_json",
                table: "grade_snapshots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "thumbnail_path",
                table: "submission_files");

            migrationBuilder.AlterColumn<string>(
                name: "payload_json",
                table: "grade_snapshots",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
