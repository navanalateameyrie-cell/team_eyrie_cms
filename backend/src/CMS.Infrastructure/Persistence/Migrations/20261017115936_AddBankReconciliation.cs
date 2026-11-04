using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_statement_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedInvoiceCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedAdvanceCount = table.Column<int>(type: "integer", nullable: false),
                    IgnoredCount = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AmountInr = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MatchedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedAdvanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bank_statement_lines_bank_statement_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "bank_statement_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_batches_CreatedAtUtc",
                table: "bank_statement_batches",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_lines_BatchId",
                table: "bank_statement_lines",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_lines_MatchedInvoiceId",
                table: "bank_statement_lines",
                column: "MatchedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_lines_Status",
                table: "bank_statement_lines",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_statement_lines");

            migrationBuilder.DropTable(
                name: "bank_statement_batches");
        }
    }
}
