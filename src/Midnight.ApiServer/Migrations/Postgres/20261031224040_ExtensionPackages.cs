using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midnight.ApiServer.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ExtensionPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstallSource",
                table: "Extensions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "file");

            migrationBuilder.AddColumn<string>(
                name: "PackageFileName",
                table: "Extensions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Extensions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "Extensions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstallSource",
                table: "Extensions");

            migrationBuilder.DropColumn(
                name: "PackageFileName",
                table: "Extensions");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Extensions");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "Extensions");
        }
    }
}
