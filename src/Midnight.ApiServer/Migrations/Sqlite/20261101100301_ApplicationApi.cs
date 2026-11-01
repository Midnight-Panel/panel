using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midnight.ApiServer.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ApplicationApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Users",
                type: "TEXT",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Servers",
                type: "TEXT",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PermissionsJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    AllowedIps = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationKeys_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                table: "Users",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servers_ExternalId",
                table: "Servers",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationKeys_CreatedById",
                table: "ApplicationKeys",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationKeys_Identifier",
                table: "ApplicationKeys",
                column: "Identifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationKeys");

            migrationBuilder.DropIndex(
                name: "IX_Users_ExternalId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Servers_ExternalId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Servers");
        }
    }
}
