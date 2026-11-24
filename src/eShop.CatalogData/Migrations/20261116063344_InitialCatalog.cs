using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace eShop.CatalogData.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_brand",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    brand = table.Column<string>(type: "varchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_brand", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_initialization",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    initialized_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_initialization", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "varchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "varchar(50)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    picture_file_name = table.Column<string>(type: "text", nullable: false),
                    catalog_type_id = table.Column<int>(type: "integer", nullable: false),
                    catalog_brand_id = table.Column<int>(type: "integer", nullable: false),
                    available_stock = table.Column<int>(type: "integer", nullable: false),
                    restock_threshold = table.Column<int>(type: "integer", nullable: false),
                    max_stock_threshold = table.Column<int>(type: "integer", nullable: false),
                    on_reorder = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalog_catalog_brand_catalog_brand_id",
                        column: x => x.catalog_brand_id,
                        principalTable: "catalog_brand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_catalog_catalog_type_catalog_type_id",
                        column: x => x.catalog_type_id,
                        principalTable: "catalog_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_initialization");

            migrationBuilder.DropTable(
                name: "catalog_brand");

            migrationBuilder.DropTable(
                name: "catalog_type");
        }
    }
}
