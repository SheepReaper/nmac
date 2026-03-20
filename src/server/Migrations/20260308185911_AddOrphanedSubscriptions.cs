using System;

using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddOrphanedSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orphaned_subscriptions",
                columns: table => new
                {
                    callback_uri = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<Guid>(type: "uuid", nullable: true),
                    topic_uri = table.Column<string>(type: "text", nullable: false),
                    secret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    expiration = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orphaned_subscriptions", x => x.callback_uri);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orphaned_subscriptions_slug",
                table: "orphaned_subscriptions",
                column: "slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orphaned_subscriptions");
        }
    }
}
