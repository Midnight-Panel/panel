using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midnight.ApiServer.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ServerFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Backups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    IgnoredFiles = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Backups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Backups_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseHosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    NodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxDatabases = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseHosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatabaseHosts_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Minute = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Hour = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DayOfMonth = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Month = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DayOfWeek = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    OnlyWhenOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsProcessing = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastRunAt = table.Column<long>(type: "INTEGER", nullable: true),
                    NextRunAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Event = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PropertiesJson = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerActivities_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerActivities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Subusers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PermissionsJson = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subusers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subusers_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subusers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerDatabases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseHostId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Remote = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerDatabases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerDatabases_DatabaseHosts_DatabaseHostId",
                        column: x => x.DatabaseHostId,
                        principalTable: "DatabaseHosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerDatabases_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    TimeOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    ContinueOnFailure = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleTasks_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Backups_ServerId",
                table: "Backups",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Backups_Uuid",
                table: "Backups",
                column: "Uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseHosts_Name",
                table: "DatabaseHosts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseHosts_NodeId",
                table: "DatabaseHosts",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTasks_ScheduleId_Sequence",
                table: "ScheduleTasks",
                columns: new[] { "ScheduleId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_IsActive_NextRunAt",
                table: "Schedules",
                columns: new[] { "IsActive", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ServerId",
                table: "Schedules",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerActivities_ServerId_CreatedAt",
                table: "ServerActivities",
                columns: new[] { "ServerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServerActivities_UserId",
                table: "ServerActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerDatabases_DatabaseHostId_Name",
                table: "ServerDatabases",
                columns: new[] { "DatabaseHostId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerDatabases_DatabaseHostId_Username",
                table: "ServerDatabases",
                columns: new[] { "DatabaseHostId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerDatabases_ServerId",
                table: "ServerDatabases",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Subusers_ServerId_UserId",
                table: "Subusers",
                columns: new[] { "ServerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subusers_UserId",
                table: "Subusers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Backups");

            migrationBuilder.DropTable(
                name: "ScheduleTasks");

            migrationBuilder.DropTable(
                name: "ServerActivities");

            migrationBuilder.DropTable(
                name: "ServerDatabases");

            migrationBuilder.DropTable(
                name: "Subusers");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "DatabaseHosts");
        }
    }
}
