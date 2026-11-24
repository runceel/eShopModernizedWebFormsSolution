namespace eShop.CatalogData;

public sealed class CatalogType
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public ICollection<Catalog> Catalogs { get; } = new List<Catalog>();
}
