using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class AddYTVideosAndContentDistributionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "headers",
                table: "content_distributions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_received_at",
                table: "content_distributions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "content_distributions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "yt_videos",
                columns: table => new
                {
                    video_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    channel_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    topic_uri = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    watch_url = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_yt_videos", x => x.video_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_yt_videos_channel_id_updated_at",
                table: "yt_videos",
                columns: new[] { "channel_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_yt_videos_topic_uri",
                table: "yt_videos",
                column: "topic_uri");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "yt_videos");

            migrationBuilder.DropColumn(
                name: "headers",
                table: "content_distributions");

            migrationBuilder.DropColumn(
                name: "last_received_at",
                table: "content_distributions");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "content_distributions");
        }
    }
}
