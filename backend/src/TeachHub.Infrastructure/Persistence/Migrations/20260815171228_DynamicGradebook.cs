using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DynamicGradebook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "bonus_default_column_id",
                table: "course_offerings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bonus_default_target",
                table: "course_offerings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "bonus_max_points_per_student",
                table: "course_offerings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grade_conditions",
                table: "course_offerings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "grade_scheme_template_id",
                table: "course_offerings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bonus_point_reason_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    default_points = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    default_target = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    default_column_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bonus_point_reason_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_bonus_point_reason_templates_asp_net_users_instructor_id",
                        column: x => x.instructor_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_columns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    weight_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    aggregation = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_columns", x => x.id);
                    table.CheckConstraint("ck_grade_column_key_slug", "key ~ '^[a-z0-9_]+$'");
                    table.ForeignKey(
                        name: "fk_grade_columns_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_export_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_export_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_export_templates_asp_net_users_instructor_id",
                        column: x => x.instructor_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_scheme_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_scheme_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_scheme_templates_asp_net_users_instructor_id",
                        column: x => x.instructor_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    taken_by = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_snapshots_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bonus_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_column_id = table.Column<Guid>(type: "uuid", nullable: true),
                    points = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    context_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    context_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bonus_points", x => x.id);
                    table.CheckConstraint("ck_bonus_points_positive", "points > 0");
                    table.CheckConstraint("ck_bonus_reason_not_blank", "btrim(reason) <> ''");
                    table.CheckConstraint("ck_bonus_target_column", "(target = 'GradeColumn' AND target_column_id IS NOT NULL) OR\n(target = 'FinalTotal'  AND target_column_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_bonus_points_bonus_point_reason_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "bonus_point_reason_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bonus_points_course_offerings_offering_id",
                        column: x => x.offering_id,
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bonus_points_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bonus_points_grade_columns_target_column_id",
                        column: x => x.target_column_id,
                        principalTable: "grade_columns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "grade_column_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_column_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weight_within_column = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_column_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_column_sources_grade_columns_grade_column_id",
                        column: x => x.grade_column_id,
                        principalTable: "grade_columns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_column_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    is_manual_override = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_records_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_grade_records_grade_columns_grade_column_id",
                        column: x => x.grade_column_id,
                        principalTable: "grade_columns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_export_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    column_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    header = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    decimal_places = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_export_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_export_items_grade_export_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "grade_export_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_scheme_template_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    weight_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    aggregation = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_scheme_template_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_scheme_template_items_grade_scheme_templates_template",
                        column: x => x.template_id,
                        principalTable: "grade_scheme_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bonus_point_reason_templates_instructor_id_order_index",
                table: "bonus_point_reason_templates",
                columns: new[] { "instructor_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_bonus_points_enrollment_id_granted_at",
                table: "bonus_points",
                columns: new[] { "enrollment_id", "granted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bonus_points_offering_id",
                table: "bonus_points",
                column: "offering_id");

            migrationBuilder.CreateIndex(
                name: "ix_bonus_points_target_column_id",
                table: "bonus_points",
                column: "target_column_id");

            migrationBuilder.CreateIndex(
                name: "ix_bonus_points_template_id",
                table: "bonus_points",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_column_sources_grade_column_id_source_type_source_id",
                table: "grade_column_sources",
                columns: new[] { "grade_column_id", "source_type", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_columns_offering_id_key",
                table: "grade_columns",
                columns: new[] { "offering_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_export_items_template_id_order_index",
                table: "grade_export_items",
                columns: new[] { "template_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_grade_export_templates_instructor_id_name",
                table: "grade_export_templates",
                columns: new[] { "instructor_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_records_enrollment_id_grade_column_id",
                table: "grade_records",
                columns: new[] { "enrollment_id", "grade_column_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_records_grade_column_id",
                table: "grade_records",
                column: "grade_column_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_scheme_template_items_template_id_key",
                table: "grade_scheme_template_items",
                columns: new[] { "template_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_scheme_templates_instructor_id_name",
                table: "grade_scheme_templates",
                columns: new[] { "instructor_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_snapshots_offering_id_taken_at",
                table: "grade_snapshots",
                columns: new[] { "offering_id", "taken_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bonus_points");

            migrationBuilder.DropTable(
                name: "grade_column_sources");

            migrationBuilder.DropTable(
                name: "grade_export_items");

            migrationBuilder.DropTable(
                name: "grade_records");

            migrationBuilder.DropTable(
                name: "grade_scheme_template_items");

            migrationBuilder.DropTable(
                name: "grade_snapshots");

            migrationBuilder.DropTable(
                name: "bonus_point_reason_templates");

            migrationBuilder.DropTable(
                name: "grade_export_templates");

            migrationBuilder.DropTable(
                name: "grade_columns");

            migrationBuilder.DropTable(
                name: "grade_scheme_templates");

            migrationBuilder.DropColumn(
                name: "bonus_default_column_id",
                table: "course_offerings");

            migrationBuilder.DropColumn(
                name: "bonus_default_target",
                table: "course_offerings");

            migrationBuilder.DropColumn(
                name: "bonus_max_points_per_student",
                table: "course_offerings");

            migrationBuilder.DropColumn(
                name: "grade_conditions",
                table: "course_offerings");

            migrationBuilder.DropColumn(
                name: "grade_scheme_template_id",
                table: "course_offerings");
        }
    }
}
