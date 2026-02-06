namespace SiteWeb.Api.Tenancy;

public class TenantContext
{
    public string TenantId { get; private set; } = "default";

    public void SetTenant(string tenantId)
    {
        TenantId = string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId;
    }
}
