using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tds_challans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallanNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DepositDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BankBsrCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Section = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TotalAmountInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tds_challans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tds_challans_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tds_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeductionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Section = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeducteeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DeducteePan = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: true),
                    GrossAmountInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TdsRatePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TdsAmountInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    FinancialYear = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    ChallanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tds_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tds_entries_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tds_entries_tds_challans_ChallanId",
                        column: x => x.ChallanId,
                        principalTable: "tds_challans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tds_challans_ClientId",
                table: "tds_challans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_tds_challans_ClientId_ChallanNumber",
                table: "tds_challans",
                columns: new[] { "ClientId", "ChallanNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tds_entries_ChallanId",
                table: "tds_entries",
                column: "ChallanId");

            migrationBuilder.CreateIndex(
                name: "IX_tds_entries_ClientId",
                table: "tds_entries",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_tds_entries_ClientId_FinancialYear_Quarter_Section",
                table: "tds_entries",
                columns: new[] { "ClientId", "FinancialYear", "Quarter", "Section" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tds_entries");

            migrationBuilder.DropTable(
                name: "tds_challans");
        }
    }
}
