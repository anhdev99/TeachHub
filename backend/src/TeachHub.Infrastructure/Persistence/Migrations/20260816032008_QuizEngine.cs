using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuizEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "subject_version_id",
                table: "quizzes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "config",
                table: "quizzes",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "offering_id",
                table: "quizzes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    raw_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    normalized_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    wrong_question_ids = table.Column<string>(type: "jsonb", nullable: false),
                    question_order = table.Column<string>(type: "jsonb", nullable: false),
                    answers = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_quiz_attempts_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quiz_attempts_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    foundation_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    points = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    allow_partial_credit = table.Column<bool>(type: "boolean", nullable: false),
                    fill_blank = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_quiz_questions_asp_net_users_instructor_id",
                        column: x => x.instructor_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_options_quiz_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    points_override = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_quiz_items_quiz_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_quiz_items_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quizzes_offering_id",
                table: "quizzes",
                column: "offering_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quizzes_owner",
                table: "quizzes",
                sql: "(subject_version_id IS NOT NULL AND offering_id IS NULL) OR\n(subject_version_id IS NULL     AND offering_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_question_options_question_id_order_index",
                table: "question_options",
                columns: new[] { "question_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_enrollment_id",
                table: "quiz_attempts",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_quiz_id_enrollment_id_attempt_no",
                table: "quiz_attempts",
                columns: new[] { "quiz_id", "enrollment_id", "attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quiz_items_question_id",
                table: "quiz_items",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_items_quiz_id_question_id",
                table: "quiz_items",
                columns: new[] { "quiz_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quiz_questions_instructor_id",
                table: "quiz_questions",
                column: "instructor_id");

            migrationBuilder.AddForeignKey(
                name: "fk_quizzes_course_offerings_offering_id",
                table: "quizzes",
                column: "offering_id",
                principalTable: "course_offerings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_quizzes_course_offerings_offering_id",
                table: "quizzes");

            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "quiz_attempts");

            migrationBuilder.DropTable(
                name: "quiz_items");

            migrationBuilder.DropTable(
                name: "quiz_questions");

            migrationBuilder.DropIndex(
                name: "ix_quizzes_offering_id",
                table: "quizzes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quizzes_owner",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "config",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "offering_id",
                table: "quizzes");

            migrationBuilder.AlterColumn<Guid>(
                name: "subject_version_id",
                table: "quizzes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
