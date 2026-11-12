using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceDesk.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetTag = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false, collation: "NOCASE"),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 63, nullable: false),
                    BitLockerState = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TpmState = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AssignedTechnician = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    NewStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Technician = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEvents_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Assets",
                columns: new[] { "Id", "AssetTag", "BitLockerState", "Hostname", "LastCheckedAtUtc", "TpmState" },
                values: new object[,]
                {
                    { 1, "WS-001", "suspended", "FIN-WS-01", new DateTime(2026, 11, 1, 13, 45, 0, 0, DateTimeKind.Utc), "ready" },
                    { 2, "WS-002", "protected", "FIN-WS-02", new DateTime(2026, 11, 1, 13, 30, 0, 0, DateTimeKind.Utc), "not-ready" },
                    { 3, "WS-003", "protected", "OPS-WS-01", new DateTime(2026, 11, 1, 13, 15, 0, 0, DateTimeKind.Utc), "ready" },
                    { 4, "WS-004", "unknown", "OPS-WS-02", new DateTime(2026, 11, 1, 13, 0, 0, 0, DateTimeKind.Utc), "unknown" },
                    { 5, "WS-005", "protected", "HR-WS-01", new DateTime(2026, 11, 1, 12, 45, 0, 0, DateTimeKind.Utc), "ready" },
                    { 6, "WS-006", "protected", "HR-WS-02", new DateTime(2026, 11, 1, 12, 30, 0, 0, DateTimeKind.Utc), "ready" },
                    { 7, "WS-007", "off", "ENG-WS-01", new DateTime(2026, 11, 1, 12, 15, 0, 0, DateTimeKind.Utc), "ready" },
                    { 8, "WS-008", "protected", "ENG-WS-02", new DateTime(2026, 11, 1, 12, 0, 0, 0, DateTimeKind.Utc), "ready" },
                    { 9, "WS-009", "protected", "SALES-WS-01", new DateTime(2026, 11, 1, 11, 45, 0, 0, DateTimeKind.Utc), "ready" },
                    { 10, "WS-010", "protected", "DESIGN-WS-01", new DateTime(2026, 11, 1, 11, 30, 0, 0, DateTimeKind.Utc), "ready" },
                    { 11, "WS-011", "protected", "SUPPORT-WS-01", new DateTime(2026, 11, 1, 11, 15, 0, 0, DateTimeKind.Utc), "ready" },
                    { 12, "WS-012", "unknown", "TRAINING-WS-01", new DateTime(2026, 11, 1, 11, 0, 0, 0, DateTimeKind.Utc), "missing" }
                });

            migrationBuilder.InsertData(
                table: "Tickets",
                columns: new[] { "Id", "AssetId", "AssignedTechnician", "CreatedAtUtc", "Priority", "ResolvedAtUtc", "Status", "Summary" },
                values: new object[,]
                {
                    { 1, 1, null, new DateTime(2026, 11, 1, 8, 0, 0, 0, DateTimeKind.Utc), "P2", null, "open", "BitLocker protection suspended after restart" },
                    { 2, 2, "Alex Morgan", new DateTime(2026, 11, 1, 2, 0, 0, 0, DateTimeKind.Utc), "P2", null, "in-progress", "TPM is not ready after firmware update" },
                    { 3, 3, null, new DateTime(2026, 10, 31, 20, 0, 0, 0, DateTimeKind.Utc), "P1", null, "open", "Workstation cannot connect to the office network" },
                    { 4, 4, null, new DateTime(2026, 10, 31, 14, 0, 0, 0, DateTimeKind.Utc), "P3", null, "open", "External monitor flickers after sign-in" },
                    { 5, 5, "Alex Morgan", new DateTime(2026, 10, 31, 8, 0, 0, 0, DateTimeKind.Utc), "P4", new DateTime(2026, 10, 31, 11, 0, 0, 0, DateTimeKind.Utc), "resolved", "Requested headset replacement" },
                    { 6, 6, "Alex Morgan", new DateTime(2026, 10, 31, 2, 0, 0, 0, DateTimeKind.Utc), "P3", null, "in-progress", "Shared printer missing after restart" },
                    { 7, 7, null, new DateTime(2026, 10, 30, 20, 0, 0, 0, DateTimeKind.Utc), "P2", null, "open", "BitLocker check reports protection off" },
                    { 8, 8, "Alex Morgan", new DateTime(2026, 10, 30, 14, 0, 0, 0, DateTimeKind.Utc), "P2", new DateTime(2026, 10, 30, 16, 0, 0, 0, DateTimeKind.Utc), "resolved", "USB dock drops network connection" },
                    { 9, 9, null, new DateTime(2026, 10, 30, 8, 0, 0, 0, DateTimeKind.Utc), "P3", null, "open", "New starter needs a workstation" },
                    { 10, 10, "Alex Morgan", new DateTime(2026, 10, 30, 2, 0, 0, 0, DateTimeKind.Utc), "P2", null, "in-progress", "Disk space alert on design workstation" },
                    { 11, 11, "Alex Morgan", new DateTime(2026, 10, 29, 20, 0, 0, 0, DateTimeKind.Utc), "P3", new DateTime(2026, 10, 30, 1, 0, 0, 0, DateTimeKind.Utc), "resolved", "Video calls have no audio input" },
                    { 12, 12, null, new DateTime(2026, 10, 29, 14, 0, 0, 0, DateTimeKind.Utc), "P4", null, "open", "Screen timeout setting requested" },
                    { 13, 3, "Alex Morgan", new DateTime(2026, 10, 29, 8, 0, 0, 0, DateTimeKind.Utc), "P3", null, "in-progress", "Keyboard disconnects during sign-in" },
                    { 14, 4, "Alex Morgan", new DateTime(2026, 10, 29, 2, 0, 0, 0, DateTimeKind.Utc), "P2", new DateTime(2026, 10, 29, 6, 0, 0, 0, DateTimeKind.Utc), "resolved", "Encryption check has not reported" },
                    { 15, 5, null, new DateTime(2026, 10, 28, 20, 0, 0, 0, DateTimeKind.Utc), "P4", null, "open", "Update pending restart" },
                    { 16, 6, "Alex Morgan", new DateTime(2026, 10, 28, 14, 0, 0, 0, DateTimeKind.Utc), "P1", null, "in-progress", "Workstation is stuck at the sign-in screen" },
                    { 17, 7, "Alex Morgan", new DateTime(2026, 10, 28, 8, 0, 0, 0, DateTimeKind.Utc), "P4", new DateTime(2026, 10, 28, 11, 0, 0, 0, DateTimeKind.Utc), "resolved", "Install approved PDF reader" },
                    { 18, 8, "Alex Morgan", new DateTime(2026, 10, 28, 2, 0, 0, 0, DateTimeKind.Utc), "P4", null, "in-progress", "Confirm replacement mouse delivery" },
                    { 19, 9, null, new DateTime(2026, 10, 27, 20, 0, 0, 0, DateTimeKind.Utc), "P2", null, "open", "VPN connection drops on Wi-Fi" },
                    { 20, 10, null, new DateTime(2026, 10, 27, 14, 0, 0, 0, DateTimeKind.Utc), "P4", null, "open", "Request for a second monitor" }
                });

            migrationBuilder.InsertData(
                table: "TicketEvents",
                columns: new[] { "Id", "NewStatus", "Note", "OccurredAtUtc", "PreviousStatus", "Technician", "TicketId" },
                values: new object[,]
                {
                    { 1, "in-progress", null, new DateTime(2026, 11, 1, 3, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 2 },
                    { 2, "in-progress", null, new DateTime(2026, 10, 31, 9, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 5 },
                    { 3, "resolved", "Verified the reported issue and recorded the outcome.", new DateTime(2026, 10, 31, 11, 0, 0, 0, DateTimeKind.Utc), "in-progress", "Alex Morgan", 5 },
                    { 4, "in-progress", null, new DateTime(2026, 10, 31, 3, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 6 },
                    { 5, "in-progress", null, new DateTime(2026, 10, 30, 15, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 8 },
                    { 6, "resolved", "Verified the reported issue and recorded the outcome.", new DateTime(2026, 10, 30, 16, 0, 0, 0, DateTimeKind.Utc), "in-progress", "Alex Morgan", 8 },
                    { 7, "in-progress", null, new DateTime(2026, 10, 30, 3, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 10 },
                    { 8, "in-progress", null, new DateTime(2026, 10, 29, 21, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 11 },
                    { 9, "resolved", "Verified the reported issue and recorded the outcome.", new DateTime(2026, 10, 30, 1, 0, 0, 0, DateTimeKind.Utc), "in-progress", "Alex Morgan", 11 },
                    { 10, "in-progress", null, new DateTime(2026, 10, 29, 9, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 13 },
                    { 11, "in-progress", null, new DateTime(2026, 10, 29, 3, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 14 },
                    { 12, "resolved", "Verified the reported issue and recorded the outcome.", new DateTime(2026, 10, 29, 6, 0, 0, 0, DateTimeKind.Utc), "in-progress", "Alex Morgan", 14 },
                    { 13, "in-progress", null, new DateTime(2026, 10, 28, 15, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 16 },
                    { 14, "in-progress", null, new DateTime(2026, 10, 28, 9, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 17 },
                    { 15, "resolved", "Verified the reported issue and recorded the outcome.", new DateTime(2026, 10, 28, 11, 0, 0, 0, DateTimeKind.Utc), "in-progress", "Alex Morgan", 17 },
                    { 16, "in-progress", null, new DateTime(2026, 10, 28, 3, 0, 0, 0, DateTimeKind.Utc), "open", "Alex Morgan", 18 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetTag",
                table: "Assets",
                column: "AssetTag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketEvents_TicketId",
                table: "TicketEvents",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssetId",
                table: "Tickets",
                column: "AssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketEvents");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
