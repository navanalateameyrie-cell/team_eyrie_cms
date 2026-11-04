using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNumber = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: false),
                    LegacyFileNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_files_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_files_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_files_service_definitions_ServiceDefinitionId",
                        column: x => x.ServiceDefinitionId,
                        principalTable: "service_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "client_code_counters",
                columns: new[] { "Name", "NextValue" },
                values: new object[] { "service_file", 1000000000L });

            migrationBuilder.CreateIndex(
                name: "IX_service_files_BranchId",
                table: "service_files",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_service_files_ClientId",
                table: "service_files",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_service_files_FileNumber",
                table: "service_files",
                column: "FileNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_files_LegacyFileNumber",
                table: "service_files",
                column: "LegacyFileNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_files_ServiceDefinitionId",
                table: "service_files",
                column: "ServiceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_service_files_Status",
                table: "service_files",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_files");

            migrationBuilder.DeleteData(
                table: "client_code_counters",
                keyColumn: "Name",
                keyValue: "service_file");
        }
    }
}
