using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrityGamificationAndReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "days_late",
                table: "submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "hint_penalty_percent",
                table: "submissions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "late_penalty_percent",
                table: "submissions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "raw_score",
                table: "submissions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    icon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    logic = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    conditions = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_badges", x => x.id);
                    table.CheckConstraint("ck_badges_code_slug", "code ~ '^[a-z0-9_]+$'");
                    table.ForeignKey(
                        name: "fk_badges_asp_net_users_instructor_id",
                        column: x => x.instructor_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hint_unlocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_hint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    penalty_percent_applied = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hint_unlocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_hint_unlocks_assignment_hints_assignment_hint_id",
                        column: x => x.assignment_hint_id,
                        principalTable: "assignment_hints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hint_unlocks_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offering_gamification_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    leaderboard_visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    leaderboard_size = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offering_gamification_settings", x => x.id);
                    table.CheckConstraint("ck_offering_gamification_leaderboard_size", "leaderboard_size > 0");
                    table.ForeignKey(
                        name: "fk_offering_gamification_settings_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_generation_marks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    items_created = table.Column<int>(type: "integer", nullable: false),
                    items_reset = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_generation_marks", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_generation_marks_quiz_attempts_quiz_attempt_id",
                        column: x => x.quiz_attempt_id,
                        principalTable: "quiz_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_queue_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_quiz_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    next_review_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correct_count = table.Column<int>(type: "integer", nullable: false),
                    wrong_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_queue_items", x => x.id);
                    table.CheckConstraint("ck_review_queue_items_counts_not_negative", "correct_count >= 0 AND wrong_count >= 0");
                    table.CheckConstraint("ck_review_queue_items_stage_status", "(status = 'Pending'  AND stage <> 'Mastered' AND next_review_at IS NOT NULL) OR\n(status = 'Mastered' AND stage =  'Mastered' AND next_review_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_review_queue_items_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_queue_items_quiz_attempts_source_quiz_attempt_id",
                        column: x => x.source_quiz_attempt_id,
                        principalTable: "quiz_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_review_queue_items_quiz_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_fingerprints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    hash = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    length = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_fingerprints", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_fingerprints_submission_files_submission_file_id",
                        column: x => x.submission_file_id,
                        principalTable: "submission_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_member_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: true),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_member_scores", x => x.id);
                    table.CheckConstraint("ck_submission_member_scores_range", "score >= 0 AND score <= 10");
                    table.CheckConstraint("ck_submission_member_scores_reason_not_blank", "length(btrim(reason)) > 0");
                    table.ForeignKey(
                        name: "fk_submission_member_scores_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_member_scores_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "xp_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_xp_events", x => x.id);
                    table.CheckConstraint("ck_xp_events_points_positive", "points > 0");
                    table.ForeignKey(
                        name: "fk_xp_events_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "xp_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    min_value = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    daily_cap = table.Column<int>(type: "integer", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_xp_rules", x => x.id);
                    table.CheckConstraint("ck_xp_rules_daily_cap_positive", "daily_cap IS NULL OR daily_cap > 0");
                    table.CheckConstraint("ck_xp_rules_min_value_not_negative", "min_value IS NULL OR min_value >= 0");
                    table.CheckConstraint("ck_xp_rules_points_positive", "points > 0");
                    table.ForeignKey(
                        name: "fk_xp_rules_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "earned_badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    earned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_earned_badges", x => x.id);
                    table.ForeignKey(
                        name: "fk_earned_badges_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_earned_badges_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_badges_instructor_id_code",
                table: "badges",
                columns: new[] { "instructor_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_earned_badges_badge_id",
                table: "earned_badges",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "ix_earned_badges_enrollment_id_badge_id",
                table: "earned_badges",
                columns: new[] { "enrollment_id", "badge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hint_unlocks_assignment_hint_id_enrollment_id",
                table: "hint_unlocks",
                columns: new[] { "assignment_hint_id", "enrollment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hint_unlocks_enrollment_id",
                table: "hint_unlocks",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_offering_gamification_settings_offering_id",
                table: "offering_gamification_settings",
                column: "offering_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_generation_marks_quiz_attempt_id",
                table: "review_generation_marks",
                column: "quiz_attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_items_next_review_at",
                table: "review_queue_items",
                column: "next_review_at",
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_items_question_id",
                table: "review_queue_items",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_items_source_quiz_attempt_id",
                table: "review_queue_items",
                column: "source_quiz_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_items_user_id_next_review_at",
                table: "review_queue_items",
                columns: new[] { "user_id", "next_review_at" },
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_items_user_id_question_id",
                table: "review_queue_items",
                columns: new[] { "user_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_fingerprints_hash",
                table: "source_fingerprints",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_source_fingerprints_submission_file_id",
                table: "source_fingerprints",
                column: "submission_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_member_scores_enrollment_id",
                table: "submission_member_scores",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_member_scores_submission_id_enrollment_id",
                table: "submission_member_scores",
                columns: new[] { "submission_id", "enrollment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xp_events_enrollment_id_dedupe_key",
                table: "xp_events",
                columns: new[] { "enrollment_id", "dedupe_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xp_events_enrollment_id_occurred_at",
                table: "xp_events",
                columns: new[] { "enrollment_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_xp_rules_offering_id_event_type",
                table: "xp_rules",
                columns: new[] { "offering_id", "event_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "earned_badges");

            migrationBuilder.DropTable(
                name: "hint_unlocks");

            migrationBuilder.DropTable(
                name: "offering_gamification_settings");

            migrationBuilder.DropTable(
                name: "review_generation_marks");

            migrationBuilder.DropTable(
                name: "review_queue_items");

            migrationBuilder.DropTable(
                name: "source_fingerprints");

            migrationBuilder.DropTable(
                name: "submission_member_scores");

            migrationBuilder.DropTable(
                name: "xp_events");

            migrationBuilder.DropTable(
                name: "xp_rules");

            migrationBuilder.DropTable(
                name: "badges");

            migrationBuilder.DropColumn(
                name: "days_late",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "hint_penalty_percent",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "late_penalty_percent",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "raw_score",
                table: "submissions");
        }
    }
}
