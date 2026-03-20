using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveFundingDonations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_funding_donations",
                columns: table => new
                {
                    message_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    video_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    live_chat_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    author_channel_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    author_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    amount_micros = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    amount_display_string = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_comment = table.Column<string>(type: "text", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_funding_donations", x => x.message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_funding_donations_live_chat_id_published_at",
                table: "live_funding_donations",
                columns: ["live_chat_id", "published_at"]);

            migrationBuilder.CreateIndex(
                name: "ix_live_funding_donations_video_id",
                table: "live_funding_donations",
                column: "video_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_funding_donations");
        }
    }
}
