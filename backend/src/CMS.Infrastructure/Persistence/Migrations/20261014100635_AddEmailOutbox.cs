using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAddresses = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CcAddresses = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyPlainText = table.Column<string>(type: "text", nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FromName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AttachmentCount = table.Column<int>(type: "integer", nullable: false),
                    AttachmentTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_CreatedAtUtc",
                table: "email_outbox",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_InvoiceId",
                table: "email_outbox",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_Status",
                table: "email_outbox",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_outbox");
        }
    }
}
