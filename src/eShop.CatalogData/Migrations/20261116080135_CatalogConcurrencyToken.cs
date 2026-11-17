using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eShop.CatalogData.Migrations
{
    /// <inheritdoc />
    public partial class CatalogConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Npgsql maps Catalog.Version to the provider's row-version system column.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The provider-owned row-version column is not part of the application schema.
        }
    }
}
