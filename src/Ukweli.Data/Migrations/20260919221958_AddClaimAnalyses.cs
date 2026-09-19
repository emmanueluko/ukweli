using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ukweli.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claim_analyses",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    normalized_claim = table.Column<string>(type: "text", nullable: false),
                    jurisdiction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    simple_explanation = table.Column<string>(type: "text", nullable: false),
                    unknowns = table.Column<string>(type: "jsonb", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    action_is_generic = table.Column<bool>(type: "boolean", nullable: false),
                    source_ids = table.Column<string>(type: "jsonb", nullable: false),
                    source_relations = table.Column<string>(type: "jsonb", nullable: false),
                    is_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    seed_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_analyses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_analyses_user_id_created_at",
                table: "claim_analyses",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_analyses");
        }
    }
}
