using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "engagements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialYear = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    AssessmentYear = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    PeriodLabel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_engagements_service_files_ServiceFileId",
                        column: x => x.ServiceFileId,
                        principalTable: "service_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_engagements_FinancialYear",
                table: "engagements",
                column: "FinancialYear");

            migrationBuilder.CreateIndex(
                name: "IX_engagements_ServiceFileId_FinancialYear_PeriodType_PeriodLa~",
                table: "engagements",
                columns: new[] { "ServiceFileId", "FinancialYear", "PeriodType", "PeriodLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_engagements_Status",
                table: "engagements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "engagements");
        }
    }
}
