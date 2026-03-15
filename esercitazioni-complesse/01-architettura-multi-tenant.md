# 01 — Architettura Multi-Tenant Enterprise

## Scenario
`SiteWeb.Api` deve servire più tenant enterprise con isolamento logico forte, configurazioni diverse e policy di sicurezza dedicate.

## Obiettivo
Implementare una piattaforma multi-tenant robusta con supporto a:
- risoluzione tenant per host/header/claim;
- isolamento dati per tenant;
- feature flag a livello tenant;
- policy autorizzative dinamiche per tenant.

---

## Requisiti vincolanti
- Implementare `TenantResolutionPipeline` con fallback ordinato.
- Evitare leakage dati con query filter globali e test di isolamento.
- Gestire caching per tenant con namespace separato.
- Fornire bootstrap automatico tenant (seed dati + ruoli base).
- Aggiungere audit trail tenant-aware.

## Criteri di accettazione
- Suite di test di integrazione che valida isolamento end-to-end.
- Nessuna query cross-tenant in test di sicurezza.
- Feature toggle attivabile/disattivabile runtime senza deploy.
- Documentazione tecnica con diagrammi C4 livello container/component.

## Extra hard mode
- Introduci sharding per tenant premium.
- Aggiungi piano di migrazione da single-tenant a multi-tenant senza downtime.

---

## Svolgimento guidato (con commenti)

> Nota: questa è una **soluzione di riferimento commentata** da adattare alla codebase reale.

### 1) Modello Tenant e contesto di request

```csharp
public sealed class TenantContext
{
    public string TenantId { get; init; } = default!; // ID logico del tenant (es. "acme")
    public string? Plan { get; init; }                // Piano (standard/premium) utile per policy e limiti
    public string? Region { get; init; }              // Regione dati (per compliance)
}

public interface ITenantContextAccessor
{
    TenantContext? Current { get; set; }
}

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    public TenantContext? Current { get; set; } // Scoped: vive per durata request
}
```

**Commento:** `TenantContext` deve essere `Scoped`, così ogni request vede solo il proprio tenant.

---

### 2) TenantResolutionPipeline con fallback ordinato

Ordine consigliato:
1. Host (`{tenant}.api.example.com`)
2. Header (`X-Tenant-Id`)
3. Claim (`tenant_id`)
4. Fallback finale a errore (`400/401`) se non risolvibile

```csharp
public interface ITenantResolver
{
    Task<TenantContext?> ResolveAsync(HttpContext httpContext, CancellationToken ct);
}

public sealed class CompositeTenantResolver : ITenantResolver
{
    private readonly IReadOnlyList<ITenantResolver> _resolvers;

    public CompositeTenantResolver(IEnumerable<ITenantResolver> resolvers)
        => _resolvers = resolvers.ToList(); // L'ordine di registrazione definisce il fallback

    public async Task<TenantContext?> ResolveAsync(HttpContext httpContext, CancellationToken ct)
    {
        foreach (var resolver in _resolvers)
        {
            var tenant = await resolver.ResolveAsync(httpContext, ct);
            if (tenant is not null)
                return tenant; // Primo resolver valido vince
        }

        return null; // Nessuna risoluzione: middleware restituirà errore controllato
    }
}
```

```csharp
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ITenantContextAccessor accessor)
    {
        var tenant = await resolver.ResolveAsync(context, context.RequestAborted);
        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Tenant non risolto");
            return;
        }

        accessor.Current = tenant; // Tenant disponibile a tutta la pipeline successiva
        await _next(context);
    }
}
```

**Commento:** la risoluzione tenant va fatta presto nella pipeline (subito dopo correlation/logging).

---

### 3) Isolamento dati con query filter globali

```csharp
public interface ITenantOwnedEntity
{
    string TenantId { get; set; }
}

public class AppDbContext : DbContext
{
    private readonly ITenantContextAccessor _tenantAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContextAccessor tenantAccessor)
        : base(options)
    {
        _tenantAccessor = tenantAccessor;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Esempio su un'entità Order tenant-owned
        modelBuilder.Entity<Order>()
            .HasQueryFilter(o => o.TenantId == _tenantAccessor.Current!.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Forza sempre TenantId sulle nuove entità tenant-owned
        foreach (var entry in ChangeTracker.Entries<ITenantOwnedEntity>()
                     .Where(e => e.State == EntityState.Added))
        {
            entry.Entity.TenantId = _tenantAccessor.Current!.TenantId;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
```

**Commento:** il filtro globale riduce il rischio di query cross-tenant accidentali, ma servono anche test dedicati.

---

### 4) Cache namespace separato per tenant

```csharp
public sealed class TenantCache
{
    private readonly IDistributedCache _cache;
    private readonly ITenantContextAccessor _tenant;

    public TenantCache(IDistributedCache cache, ITenantContextAccessor tenant)
    {
        _cache = cache;
        _tenant = tenant;
    }

    private string Key(string rawKey)
        => $"tenant:{_tenant.Current!.TenantId}:{rawKey}"; // Evita collisioni tra tenant

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken ct)
        => _cache.SetAsync(Key(key), value, options, ct);

    public Task<byte[]?> GetAsync(string key, CancellationToken ct)
        => _cache.GetAsync(Key(key), ct);
}
```

**Commento:** il prefisso tenant va usato in modo sistematico (cache, lock distribuiti, rate limit key).

---

### 5) Bootstrap automatico tenant

Checklist bootstrap:
- creazione record tenant in tabella metadata;
- seed ruoli base (`Admin`, `Manager`, `Viewer`);
- seed feature flag default;
- seed configurazioni tecniche (quote, limit, policy);
- verifica idempotenza (esecuzione multipla non rompe lo stato).

**Commento:** il bootstrap deve essere ri-eseguibile; usa un versionamento step (`TenantBootstrapVersion`).

---

### 6) Audit trail tenant-aware

Campi minimi audit:
- `TenantId`
- `UserId`
- `Action`
- `EntityName`
- `EntityId`
- `TimestampUtc`
- `CorrelationId`

**Commento:** senza `TenantId` e `CorrelationId` l'audit perde molto valore in incident response.

---

## Piano test (obbligatorio)

### Test integrazione isolamento
1. Crea dati `Order` per tenant `A` e `B`.
2. Esegui query autenticato come tenant `A`.
3. Verifica che risultino solo record tenant `A`.

### Test sicurezza cross-tenant
1. Tenant `A` prova accesso a ID record di tenant `B`.
2. Atteso `404` (o `403` secondo policy), mai `200`.

### Test fallback resolver
1. Nessun host tenant, header presente: risoluzione da header.
2. Header assente, claim presente: risoluzione da claim.
3. Tutto assente: `400 Tenant non risolto`.

### Test cache isolation
1. Stessa chiave logica (`dashboard`) in due tenant.
2. Verifica contenuti distinti e nessuna collisione.

---

## Deliverable richiesti
- Codice pipeline multi-tenant + registrazioni DI.
- Test integrazione e sicurezza.
- Documento tecnico breve con:
  - decisioni architetturali (ADR);
  - trade-off (complessità vs sicurezza);
  - roadmap verso sharding (extra hard mode).
