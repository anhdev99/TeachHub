using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentsAndSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_late_submission",
                table: "assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "due_at",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_config",
                table: "assignments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "late_penalty_percent_per_day",
                table: "assignments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "max_score",
                table: "assignments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "rubric_id",
                table: "assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submission_mode",
                table: "assignments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "assignment_hints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    penalty_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_hints", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignment_hints_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rubrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    instructor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_template = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rubrics", x => x.id);
                    table.ForeignKey(
                        name: "fk_rubrics_asp_net_users_instructor_id",
                        column: x => x.instructor_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    final_assessment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    slot_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accepted_file_types = table.Column<List<string>>(type: "text[]", nullable: false),
                    allow_link_instead_of_file = table.Column<bool>(type: "boolean", nullable: false),
                    max_files = table.Column<int>(type: "integer", nullable: false),
                    max_file_size_mb = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    required_structure = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_slots", x => x.id);
                    table.CheckConstraint("ck_submission_slots_owner", "(owner_type = 'Assignment'      AND assignment_id IS NOT NULL AND final_assessment_id IS NULL) OR\n(owner_type = 'FinalAssessment' AND final_assessment_id IS NOT NULL AND assignment_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_submission_slots_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    final_assessment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_late = table.Column<bool>(type: "boolean", nullable: false),
                    hints_used = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    rubric_scores = table.Column<string>(type: "jsonb", nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    graded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    graded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_owner", "(owner_type = 'Assignment'      AND assignment_id IS NOT NULL AND final_assessment_id IS NULL) OR\n(owner_type = 'FinalAssessment' AND final_assessment_id IS NOT NULL AND assignment_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_submissions_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submissions_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submissions_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    max_points = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    levels = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rubric_criteria", x => x.id);
                    table.ForeignKey(
                        name: "fk_rubric_criteria_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    external_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    processing_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    processing_error = table.Column<string>(type: "text", nullable: true),
                    structure_check_passed = table.Column<bool>(type: "boolean", nullable: true),
                    missing_structure_items = table.Column<List<string>>(type: "text[]", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    file_tree = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_files_submission_slots_slot_id",
                        column: x => x.slot_id,
                        principalTable: "submission_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_files_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_rubric_id",
                table: "assignments",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_hints_assignment_id_order_index",
                table: "assignment_hints",
                columns: new[] { "assignment_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_rubric_criteria_rubric_id_order_index",
                table: "rubric_criteria",
                columns: new[] { "rubric_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_rubrics_instructor_id_is_template",
                table: "rubrics",
                columns: new[] { "instructor_id", "is_template" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_files_slot_id",
                table: "submission_files",
                column: "slot_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_files_submission_id_slot_key",
                table: "submission_files",
                columns: new[] { "submission_id", "slot_key" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_slots_assignment_id_order_index",
                table: "submission_slots",
                columns: new[] { "assignment_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_enrollment_id",
                table: "submissions",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_offering_id_status",
                table: "submissions",
                columns: new[] { "offering_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_submission_current",
                table: "submissions",
                columns: new[] { "assignment_id", "final_assessment_id", "enrollment_id", "group_id" },
                unique: true,
                filter: "is_current");

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_rubrics_rubric_id",
                table: "assignments",
                column: "rubric_id",
                principalTable: "rubrics",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assignments_rubrics_rubric_id",
                table: "assignments");

            migrationBuilder.DropTable(
                name: "assignment_hints");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "submission_files");

            migrationBuilder.DropTable(
                name: "rubrics");

            migrationBuilder.DropTable(
                name: "submission_slots");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropIndex(
                name: "ix_assignments_rubric_id",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "allow_late_submission",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "due_at",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "group_config",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "late_penalty_percent_per_day",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "max_score",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "rubric_id",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "submission_mode",
                table: "assignments");
        }
    }
}
