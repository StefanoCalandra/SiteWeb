namespace SiteWeb.Api.Tenancy;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider provider, TenantContext tenantContext, ILogger<TenantMiddleware> logger)
    {
        // Legge il tenant dalla request e lo salva nel contesto.
        var tenantId = provider.ResolveTenantId(context);
        tenantContext.SetTenant(tenantId);

        // Aggiunge il TenantId allo scope dei log.
        using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantContext.TenantId }))
        {
            await _next(context);
        }
    }
}
