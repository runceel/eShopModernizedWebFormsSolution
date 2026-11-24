using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eShop.CatalogData.Migrations
{
    /// <inheritdoc />
    public partial class CatalogResetRecoveryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "managed_image_hashes_json",
                table: "catalog_initialization",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "reset_backup_directory",
                table: "catalog_initialization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reset_image_directory",
                table: "catalog_initialization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reset_image_manifest_json",
                table: "catalog_initialization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reset_operation_id",
                table: "catalog_initialization",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reset_snapshot_json",
                table: "catalog_initialization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reset_started_at_utc",
                table: "catalog_initialization",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "catalog_initialization",
                type: "varchar(32)",
                nullable: false,
                defaultValue: "initialized");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "managed_image_hashes_json",
                table: "catalog_initialization");

            migrationBuilder.DropColumn(
                name: "reset_backup_directory",
                table: "catalog_initialization");

            migrationBuilder.DropColumn(
                name: "reset_image_directory",
                table: "catalog_initialization");

            migrationBuilder.DropColumn(
                name: "reset_image_manifest_json",
                table: "catalog_initialization");

            migrationBuilder.DropColumn(
                name: "reset_operation_id",
                table: "catalog_initialization");

            migrationBuilder.DropColumn(
                name: "reset_snapshot_json",
                table: "catalog_initialization");

            migrationBuilder.DropColumn(
                name: "reset_started_at_utc",
                table: "catalog_initialization");

            migrationBuilder.DropColumn(
                name: "status",
                table: "catalog_initialization");
        }
    }
}
