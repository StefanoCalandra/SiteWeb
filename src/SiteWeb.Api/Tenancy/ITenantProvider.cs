namespace SiteWeb.Api.Tenancy;

public interface ITenantProvider
{
    string ResolveTenantId(HttpContext httpContext);
}
