using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "avatar_cache_items",
                columns: table => new
                {
                    cache_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    content = table.Column<byte[]>(type: "bytea", nullable: true),
                    is_missing = table.Column<bool>(type: "boolean", nullable: false),
                    cached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avatar_cache_items", x => x.cache_key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_avatar_cache_items_expires_at",
                table: "avatar_cache_items",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avatar_cache_items");
        }
    }
}
