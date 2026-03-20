using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace NMAC.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_distributions",
                columns: table => new
                {
                    topic_uri = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_distributions", x => x.topic_uri);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    topic_uri = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<Guid>(type: "uuid", nullable: true),
                    secret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mode = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    expiration = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.topic_uri);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_distributions");

            migrationBuilder.DropTable(
                name: "subscriptions");
        }
    }
}
