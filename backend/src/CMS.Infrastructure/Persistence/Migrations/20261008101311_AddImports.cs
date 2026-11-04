using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceSheet = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    CommittedClientCount = table.Column<int>(type: "integer", nullable: false),
                    CommittedServiceFileCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_staging_clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    NormalizedPan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NormalizedLegalName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NormalizedTradeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NormalizedLegacyClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EntityTypeCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ClientGroupCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ValidationErrorsJson = table.Column<string>(type: "text", nullable: true),
                    CandidateClientIdsJson = table.Column<string>(type: "text", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    DecidedClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommittedClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_staging_clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_staging_clients_import_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_staging_service_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceStagingClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LegacyFileNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ValidationErrorsJson = table.Column<string>(type: "text", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    CommittedServiceFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_staging_service_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_staging_service_files_import_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_staging_service_files_import_staging_clients_SourceS~",
                        column: x => x.SourceStagingClientId,
                        principalTable: "import_staging_clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_FileSha256",
                table: "import_batches",
                column: "FileSha256");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_Status",
                table: "import_batches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_UploadedAtUtc",
                table: "import_batches",
                column: "UploadedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_clients_BatchId",
                table: "import_staging_clients",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_clients_NormalizedLegacyClientId",
                table: "import_staging_clients",
                column: "NormalizedLegacyClientId");

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_clients_NormalizedPan",
                table: "import_staging_clients",
                column: "NormalizedPan");

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_service_files_BatchId",
                table: "import_staging_service_files",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_import_staging_service_files_SourceStagingClientId",
                table: "import_staging_service_files",
                column: "SourceStagingClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_staging_service_files");

            migrationBuilder.DropTable(
                name: "import_staging_clients");

            migrationBuilder.DropTable(
                name: "import_batches");
        }
    }
}
