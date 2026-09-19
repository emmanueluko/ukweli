using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ukweli.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sources",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    issuer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    jurisdiction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    topic = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    published_at = table.Column<DateOnly>(type: "date", nullable: true),
                    checked_at = table.Column<DateOnly>(type: "date", nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    collection_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    excerpt = table.Column<string>(type: "text", nullable: false),
                    placeholder = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sources", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sources_active_jurisdiction_topic",
                table: "sources",
                columns: new[] { "active", "jurisdiction", "topic" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sources");
        }
    }
}
