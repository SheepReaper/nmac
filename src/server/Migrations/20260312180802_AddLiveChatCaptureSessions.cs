using System;

using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveChatCaptureSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_chat_capture_sessions",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_chat_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    video_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_chat_capture_sessions", x => x.session_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_chat_capture_sessions_live_chat_id",
                table: "live_chat_capture_sessions",
                column: "live_chat_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_chat_capture_sessions_state_last_attempt_at",
                table: "live_chat_capture_sessions",
                columns: new[] { "state", "last_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_live_chat_capture_sessions_video_id",
                table: "live_chat_capture_sessions",
                column: "video_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_chat_capture_sessions");
        }
    }
}
