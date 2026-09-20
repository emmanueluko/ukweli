using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ukweli.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurationProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "curation",
                table: "sources",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "human");

            migrationBuilder.AddColumn<DateOnly>(
                name: "revalidated_at",
                table: "sources",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "curation",
                table: "sources");

            migrationBuilder.DropColumn(
                name: "revalidated_at",
                table: "sources");
        }
    }
}
