using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Midnight.ApiServer.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Branding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PanelName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LogoLightFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LogoDarkFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaviconFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    LoginBackgroundType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LoginBackgroundValue = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CustomCss = table.Column<string>(type: "text", maxLength: 32768, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandingSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandingSettings");
        }
    }
}
