using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNotesAndAdvances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditedInr",
                table: "invoices",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "advances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    AmountAppliedInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsReversed = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_advances_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FinancialYear = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubtotalInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CgstInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    SgstInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    IgstInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_notes_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_notes_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "debit_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FinancialYear = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubtotalInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CgstInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    SgstInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    IgstInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debit_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_debit_notes_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_debit_notes_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_number_counters",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    FinancialYear = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_number_counters", x => new { x.Kind, x.FinancialYear });
                });

            migrationBuilder.CreateTable(
                name: "advance_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsReversed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advance_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_advance_applications_advances_AdvanceId",
                        column: x => x.AdvanceId,
                        principalTable: "advances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_advance_applications_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_advance_applications_AdvanceId",
                table: "advance_applications",
                column: "AdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_advance_applications_InvoiceId",
                table: "advance_applications",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_advances_ClientId",
                table: "advances",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_ClientId",
                table: "credit_notes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_InvoiceId",
                table: "credit_notes",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_Number",
                table: "credit_notes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_ClientId",
                table: "debit_notes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_InvoiceId",
                table: "debit_notes",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_Number",
                table: "debit_notes",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advance_applications");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropTable(
                name: "debit_notes");

            migrationBuilder.DropTable(
                name: "document_number_counters");

            migrationBuilder.DropTable(
                name: "advances");

            migrationBuilder.DropColumn(
                name: "CreditedInr",
                table: "invoices");
        }
    }
}
