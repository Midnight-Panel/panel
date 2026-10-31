using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Midnight.ApiServer.Migrations.Postgres
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Author = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShardCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Author = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DockerImagesJson = table.Column<string>(type: "text", nullable: false),
                    DefaultDockerImage = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartupCommand = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    StopCommand = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StopSignal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConfigFilesJson = table.Column<string>(type: "text", nullable: false),
                    StartupDone = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstallScript = table.Column<string>(type: "text", nullable: true),
                    InstallImage = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstallInterpreter = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FeaturesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShardId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EnvVariable = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefaultValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UserViewable = table.Column<bool>(type: "boolean", nullable: false),
                    UserEditable = table.Column<bool>(type: "boolean", nullable: false),
                    Rules = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
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
