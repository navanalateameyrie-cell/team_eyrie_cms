using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AckDateUtc",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AckNo",
                table: "invoices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EInvoiceCancelReason",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EInvoiceCancelledAtUtc",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EInvoiceStatus",
                table: "invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Irn",
                table: "invoices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrText",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_EInvoiceStatus",
                table: "invoices",
                column: "EInvoiceStatus");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Irn",
                table: "invoices",
                column: "Irn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_EInvoiceStatus",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_Irn",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "AckDateUtc",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "AckNo",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceCancelReason",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceCancelledAtUtc",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceStatus",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "Irn",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "QrText",
                table: "invoices");
        }
    }
}
