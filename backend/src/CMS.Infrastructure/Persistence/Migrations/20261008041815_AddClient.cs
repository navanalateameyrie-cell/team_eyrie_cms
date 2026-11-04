using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_code_counters",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_code_counters", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: false),
                    LegacyClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LegalName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TradeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Pan = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: true),
                    EntityTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clients_client_groups_ClientGroupId",
                        column: x => x.ClientGroupId,
                        principalTable: "client_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_clients_entity_types_EntityTypeId",
                        column: x => x.EntityTypeId,
                        principalTable: "entity_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "client_code_counters",
                columns: new[] { "Name", "NextValue" },
                values: new object[] { "client", 10000000L });

            migrationBuilder.CreateIndex(
                name: "IX_clients_ClientGroupId",
                table: "clients",
                column: "ClientGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_Code",
                table: "clients",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_EntityTypeId",
                table: "clients",
                column: "EntityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_IsActive",
                table: "clients",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_clients_LegacyClientId",
                table: "clients",
                column: "LegacyClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_LegalName",
                table: "clients",
                column: "LegalName");

            migrationBuilder.CreateIndex(
                name: "IX_clients_Pan",
                table: "clients",
                column: "Pan");

            migrationBuilder.CreateIndex(
                name: "IX_clients_TradeName",
                table: "clients",
                column: "TradeName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_code_counters");

            migrationBuilder.DropTable(
                name: "clients");
        }
    }
}
