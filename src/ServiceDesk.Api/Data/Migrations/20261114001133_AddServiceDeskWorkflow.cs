using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceDeskWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows gain optional links and start at version zero. Their ticket IDs and history stay intact.
            migrationBuilder.AlterColumn<int>(
                name: "AssetId",
                table: "Tickets",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "AssignedTechnicianId",
                table: "Tickets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Tickets",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "endpoint");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Tickets",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterId",
                table: "Tickets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Tickets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WorkstationDetails",
                table: "Tickets",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicianId",
                table: "TicketEvents",
                type: "TEXT",
                nullable: true);

            // Conversation and triage audit records are stored separately from the status history.
            migrationBuilder.CreateTable(
                name: "TicketReply",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IsInternal = table.Column<bool>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketReply", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketReply_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketReply_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketTriageEvent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                    Field = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PreviousValue = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TechnicianId = table.Column<string>(type: "TEXT", nullable: false),
                    Technician = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTriageEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTriageEvent_AspNetUsers_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketTriageEvent_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedTechnicianId",
                table: "Tickets",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequesterId",
                table: "Tickets",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEvents_TechnicianId",
                table: "TicketEvents",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketReply_AuthorId",
                table: "TicketReply",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketReply_TicketId",
                table: "TicketReply",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTriageEvent_TechnicianId",
                table: "TicketTriageEvent",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTriageEvent_TicketId",
                table: "TicketTriageEvent",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEvents_AspNetUsers_TechnicianId",
                table: "TicketEvents",
                column: "TechnicianId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_AssignedTechnicianId",
                table: "Tickets",
                column: "AssignedTechnicianId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_RequesterId",
                table: "Tickets",
                column: "RequesterId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketEvents_AspNetUsers_TechnicianId",
                table: "TicketEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_AssignedTechnicianId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_RequesterId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketReply");

            migrationBuilder.DropTable(
                name: "TicketTriageEvent");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_AssignedTechnicianId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_RequesterId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketEvents_TechnicianId",
                table: "TicketEvents");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RequesterId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "WorkstationDetails",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "TicketEvents");

            migrationBuilder.AlterColumn<int>(
                name: "AssetId",
                table: "Tickets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
