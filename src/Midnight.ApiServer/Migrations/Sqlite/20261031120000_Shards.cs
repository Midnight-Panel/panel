using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midnight.ApiServer.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Shards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShardCollections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Author = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShardCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Author = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DockerImagesJson = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultDockerImage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    StartupCommand = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    StopCommand = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StopSignal = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ConfigFilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartupDone = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    InstallScript = table.Column<string>(type: "TEXT", nullable: true),
                    InstallImage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    InstallInterpreter = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FeaturesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shards_ShardCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "ShardCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShardVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EnvVariable = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UserViewable = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Rules = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShardVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShardVariables_Shards_ShardId",
                        column: x => x.ShardId,
                        principalTable: "Shards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShardCollections_Name",
                table: "ShardCollections",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShardCollections_Uuid",
                table: "ShardCollections",
                column: "Uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shards_CollectionId_Name",
                table: "Shards",
                columns: new[] { "CollectionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shards_Uuid",
                table: "Shards",
                column: "Uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShardVariables_ShardId_EnvVariable",
                table: "ShardVariables",
                columns: new[] { "ShardId", "EnvVariable" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ShardVariables");
            migrationBuilder.DropTable(name: "Shards");
            migrationBuilder.DropTable(name: "ShardCollections");
        }
    }
}
