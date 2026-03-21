using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AlignEntityContractsA1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "author_profile_image_url",
                table: "live_super_chats",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.Sql("""
                UPDATE live_chat_capture_sessions
                SET created_at = NOW()
                WHERE created_at IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "live_chat_capture_sessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "author_profile_image_url",
                table: "live_super_chats",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "live_chat_capture_sessions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }
    }
}
