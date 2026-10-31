using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midnight.ApiServer.Migrations.Sqlite
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PanelName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LogoLightFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LogoDarkFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FaviconFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PrimaryColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    AccentColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    LoginBackgroundType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LoginBackgroundValue = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CustomCss = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: true),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
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
