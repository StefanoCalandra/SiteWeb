namespace SiteWeb.Api.Entities;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "default";
    public string ResourceName { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
