namespace eShop.CatalogData;

public sealed class CatalogBrand
{
    public int Id { get; set; }
    public string Brand { get; set; } = string.Empty;
    public ICollection<Catalog> Catalogs { get; } = new List<Catalog>();
}
