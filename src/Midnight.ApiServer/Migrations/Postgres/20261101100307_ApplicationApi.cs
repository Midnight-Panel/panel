using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Midnight.ApiServer.Migrations.Postgres
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
                type: "character varying(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Servers",
                type: "character varying(191)",
                maxLength: 191,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Identifier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PermissionsJson = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    AllowedIps = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
