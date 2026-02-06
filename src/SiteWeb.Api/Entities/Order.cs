namespace SiteWeb.Api.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Status { get; set; } = "Pending";
    public Guid CatalogItemId { get; set; }
    public int Quantity { get; set; }
    public string TenantId { get; set; } = "default";
}
