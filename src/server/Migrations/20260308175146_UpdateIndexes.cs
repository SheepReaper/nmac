using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subscriptions_callback_uri",
                table: "subscriptions");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_enabled_callback_uri_expiration",
                table: "subscriptions",
                columns: new[] { "enabled", "callback_uri", "expiration" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_slug",
                table: "subscriptions",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subscriptions_enabled_callback_uri_expiration",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_subscriptions_slug",
                table: "subscriptions");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_callback_uri",
                table: "subscriptions",
                column: "callback_uri",
                unique: true);
        }
    }
}
