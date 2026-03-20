using System;

using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSuperChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_super_chats",
                columns: table => new
                {
                    message_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    video_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    live_chat_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    author_channel_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    author_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    author_profile_image_url = table.Column<string>(type: "text", nullable: true),
                    amount_micros = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    amount_display_string = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    message_content = table.Column<string>(type: "text", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_super_chats", x => x.message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_super_chats_live_chat_id_published_at",
                table: "live_super_chats",
                columns: new[] { "live_chat_id", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_live_super_chats_video_id",
                table: "live_super_chats",
                column: "video_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_super_chats");
        }
    }
}
