using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ukweli.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The generated default was an empty string, which is not valid
            // jsonb and would have failed on any table that already had rows.
            // Existing analyses start with no translations; each language is
            // filled in the first time somebody asks to read it in that language.
            migrationBuilder.AddColumn<string>(
                name: "translations",
                table: "claim_analyses",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            // The model carries no default, so the column should not keep one
            // once the existing rows are backfilled.
            migrationBuilder.Sql(
                "ALTER TABLE claim_analyses ALTER COLUMN translations DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "translations",
                table: "claim_analyses");
        }
    }
}
