using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midnight.ApiServer.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Extensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Extensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    InstallSource = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "file"),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    PackageFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigSchemaJson = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: true),
                    ConfigValuesJson = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: true),
                    ReadmeMarkdown = table.Column<string>(type: "TEXT", nullable: true),
                    InstalledAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
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
        }
    }
}
