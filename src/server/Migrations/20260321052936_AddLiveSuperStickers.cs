using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSuperStickers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_super_sticker",
                table: "live_super_chats",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "sticker_alt_text",
                table: "live_super_chats",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sticker_alt_text_language",
                table: "live_super_chats",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sticker_id",
                table: "live_super_chats",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_super_sticker",
                table: "live_super_chats");

            migrationBuilder.DropColumn(
                name: "sticker_alt_text",
                table: "live_super_chats");

            migrationBuilder.DropColumn(
                name: "sticker_alt_text_language",
                table: "live_super_chats");

            migrationBuilder.DropColumn(
                name: "sticker_id",
                table: "live_super_chats");
        }
    }
}
