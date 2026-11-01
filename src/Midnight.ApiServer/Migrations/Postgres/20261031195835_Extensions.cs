using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Midnight.ApiServer.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Extensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CustomCss",
                table: "BrandingSettings",
                type: "character varying(32768)",
                maxLength: 32768,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 32768,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Extensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Identifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Author = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigSchemaJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: true),
                    ConfigValuesJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: true),
                    ReadmeMarkdown = table.Column<string>(type: "text", nullable: true),
                    InstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Extensions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Extensions_Category",
                table: "Extensions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Extensions_Identifier",
                table: "Extensions",
                column: "Identifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Extensions");

            migrationBuilder.AlterColumn<string>(
                name: "CustomCss",
                table: "BrandingSettings",
                type: "text",
                maxLength: 32768,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32768)",
                oldMaxLength: 32768,
                oldNullable: true);
        }
    }
}
