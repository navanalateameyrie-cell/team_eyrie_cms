using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent drop: the global unique index was removed via raw SQL during the Phase-11
            // import run on some environments. Use IF EXISTS so this migration applies cleanly on
            // both fresh and already-patched databases.
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_service_files_LegacyFileNumber""");

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "service_files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionalFieldsJson",
                table: "service_definitions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredFieldsJson",
                table: "service_definitions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "service_files");

            migrationBuilder.DropColumn(
                name: "OptionalFieldsJson",
                table: "service_definitions");

            migrationBuilder.DropColumn(
                name: "RequiredFieldsJson",
                table: "service_definitions");

            migrationBuilder.CreateIndex(
                name: "IX_service_files_LegacyFileNumber",
                table: "service_files",
                column: "LegacyFileNumber",
                unique: true);
        }
    }
}
