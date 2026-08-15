using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupsGradingAndFinalAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "final_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    grade_column_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submission_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ref_quiz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    group_config = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_final_assessments_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_final_assessments_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grade_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_value = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    new_value = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_audit_logs_asp_net_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grade_audit_logs_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grade_audit_logs_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_groups_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_members_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_members_student_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "student_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_final_assessment_id",
                table: "submissions",
                column: "final_assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_group_id",
                table: "submissions",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_slots_final_assessment_id",
                table: "submission_slots",
                column: "final_assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_assessments_offering_id",
                table: "final_assessments",
                column: "offering_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_assessments_rubric_id",
                table: "final_assessments",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_audit_logs_changed_by",
                table: "grade_audit_logs",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_grade_audit_logs_enrollment_id",
                table: "grade_audit_logs",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_audit_logs_offering_id_changed_at",
                table: "grade_audit_logs",
                columns: new[] { "offering_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_grade_audit_logs_target_type_target_id_changed_at",
                table: "grade_audit_logs",
                columns: new[] { "target_type", "target_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_group_members_enrollment_id",
                table: "group_members",
                column: "enrollment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_members_group_id",
                table: "group_members",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_groups_offering_id_name",
                table: "student_groups",
                columns: new[] { "offering_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_submission_slots_final_assessments_final_assessment_id",
                table: "submission_slots",
                column: "final_assessment_id",
                principalTable: "final_assessments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_submissions_final_assessments_final_assessment_id",
                table: "submissions",
                column: "final_assessment_id",
                principalTable: "final_assessments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_submissions_student_groups_group_id",
                table: "submissions",
                column: "group_id",
                principalTable: "student_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_submission_slots_final_assessments_final_assessment_id",
                table: "submission_slots");

            migrationBuilder.DropForeignKey(
                name: "fk_submissions_final_assessments_final_assessment_id",
                table: "submissions");

            migrationBuilder.DropForeignKey(
                name: "fk_submissions_student_groups_group_id",
                table: "submissions");

            migrationBuilder.DropTable(
                name: "final_assessments");

            migrationBuilder.DropTable(
                name: "grade_audit_logs");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "student_groups");

            migrationBuilder.DropIndex(
                name: "ix_submissions_final_assessment_id",
                table: "submissions");

            migrationBuilder.DropIndex(
                name: "ix_submissions_group_id",
                table: "submissions");

            migrationBuilder.DropIndex(
                name: "ix_submission_slots_final_assessment_id",
                table: "submission_slots");
        }
    }
}
