using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelLivePollTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_live_poll_targets",
                columns: table => new
                {
                    handle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channel_live_poll_targets", x => x.handle);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channel_live_poll_targets_enabled",
                table: "channel_live_poll_targets",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "ix_channel_live_poll_targets_updated_at",
                table: "channel_live_poll_targets",
                column: "updated_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_live_poll_targets");
        }
    }
}
