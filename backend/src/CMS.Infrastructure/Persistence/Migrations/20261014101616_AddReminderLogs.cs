using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reminder_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SentOnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssigneeStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OutboxEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reminder_logs_compliance_obligations_ObligationId",
                        column: x => x.ObligationId,
                        principalTable: "compliance_obligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_ObligationId_Kind_SentOnDate",
                table: "reminder_logs",
                columns: new[] { "ObligationId", "Kind", "SentOnDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_SentOnDate",
                table: "reminder_logs",
                column: "SentOnDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminder_logs");
        }
    }
}
