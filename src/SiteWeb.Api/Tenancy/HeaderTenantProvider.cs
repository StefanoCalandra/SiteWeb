namespace SiteWeb.Api.Tenancy;

public class HeaderTenantProvider : ITenantProvider
{
    public string ResolveTenantId(HttpContext httpContext)
    {
        return httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var value)
            ? value.ToString()
            : "default";
    }
}
