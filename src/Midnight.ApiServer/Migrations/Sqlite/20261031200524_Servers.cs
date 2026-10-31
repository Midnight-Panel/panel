using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midnight.ApiServer.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Servers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    NodeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShardId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryAllocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    DockerImage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    StartupCommand = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Cpu = table.Column<int>(type: "INTEGER", nullable: false),
                    Memory = table.Column<int>(type: "INTEGER", nullable: false),
                    Swap = table.Column<int>(type: "INTEGER", nullable: false),
                    Disk = table.Column<int>(type: "INTEGER", nullable: false),
                    IoWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    Threads = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    OomKillerDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DatabaseLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    BackupLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Suspended = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    InstalledAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Servers_Allocations_PrimaryAllocationId",
                        column: x => x.PrimaryAllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Servers_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Servers_Shards_ShardId",
                        column: x => x.ShardId,
                        principalTable: "Shards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Servers_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServerVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShardVariableId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerVariables_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerVariables_ShardVariables_ShardVariableId",
                        column: x => x.ShardVariableId,
                        principalTable: "ShardVariables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_ServerId",
                table: "Allocations",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerVariables_ServerId_ShardVariableId",
                table: "ServerVariables",
                columns: new[] { "ServerId", "ShardVariableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerVariables_ShardVariableId",
                table: "ServerVariables",
                column: "ShardVariableId");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_NodeId",
                table: "Servers",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_OwnerId",
                table: "Servers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_PrimaryAllocationId",
                table: "Servers",
                column: "PrimaryAllocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servers_ShardId",
                table: "Servers",
                column: "ShardId");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_Uuid",
                table: "Servers",
                column: "Uuid",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_Servers_ServerId",
                table: "Allocations",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Allocations_Servers_ServerId",
                table: "Allocations");

            migrationBuilder.DropTable(
                name: "ServerVariables");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_ServerId",
                table: "Allocations");
        }
    }
}
