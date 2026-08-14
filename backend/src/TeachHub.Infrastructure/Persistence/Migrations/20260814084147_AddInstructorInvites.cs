using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instructor_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructor_invites", x => x.id);
                    table.ForeignKey(
                        name: "fk_instructor_invites_asp_net_users_invited_by",
                        column: x => x.invited_by,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_instructor_invites_invited_by_accepted_at",
                table: "instructor_invites",
                columns: new[] { "invited_by", "accepted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_instructor_invites_token_hash",
                table: "instructor_invites",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instructor_invites");
        }
    }
}
