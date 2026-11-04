using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DueDayOffsetDays",
                table: "service_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Frequency",
                table: "service_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "compliance_obligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    FiledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_obligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_obligations_service_files_ServiceFileId",
                        column: x => x.ServiceFileId,
                        principalTable: "service_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_obligations_staff_AssignedStaffId",
                        column: x => x.AssignedStaffId,
                        principalTable: "staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "obligation_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_obligation_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_obligation_events_compliance_obligations_ObligationId",
                        column: x => x.ObligationId,
                        principalTable: "compliance_obligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_obligations_AssignedStaffId",
                table: "compliance_obligations",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_obligations_DueDate",
                table: "compliance_obligations",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_obligations_ServiceFileId_PeriodKey",
                table: "compliance_obligations",
                columns: new[] { "ServiceFileId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_obligations_Status",
                table: "compliance_obligations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_obligation_events_CreatedAtUtc",
                table: "obligation_events",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_obligation_events_ObligationId",
                table: "obligation_events",
                column: "ObligationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "obligation_events");

            migrationBuilder.DropTable(
                name: "compliance_obligations");

            migrationBuilder.DropColumn(
                name: "DueDayOffsetDays",
                table: "service_definitions");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "service_definitions");
        }
    }
}
