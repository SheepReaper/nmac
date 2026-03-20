using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubscriptionsAddCallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "callback_uri",
                table: "subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "enabled",
                table: "subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_callback_uri",
                table: "subscriptions",
                column: "callback_uri",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subscriptions_callback_uri",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "callback_uri",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "enabled",
                table: "subscriptions");
        }
    }
}
