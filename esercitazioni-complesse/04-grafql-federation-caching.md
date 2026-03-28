# 04 — GraphQL Federation, Caching e Cost Control

## Scenario
La sezione GraphQL deve scalare con richieste complesse e prevenire query abusive.

## Obiettivo
Portare il layer GraphQL a livello enterprise con federazione, performance tuning e protezioni anti-abuso.

---

## Requisiti vincolanti
- Implementare persisted queries obbligatorie in produzione.
- Aggiungere query cost analysis con limiti dinamici per tenant.
- Ridurre N+1 con DataLoader e caching selettivo.
- Implementare invalidazione cache event-driven.
- Versionare schema federato con deprecazioni controllate.

## Criteri di accettazione
- Profiling con baseline e miglioramento misurabile (>40% latenza p95).
- Blocchi automatici su query ad alto costo.
- Test su coerenza cache in aggiornamenti concorrenti.
- Report di compatibilità schema per client legacy.

## Extra hard mode
- Introduci edge caching con chiavi composte da tenant + scope.
- Canary release di modifiche schema con osservabilità dedicata.

---

## Svolgimento guidato (con commenti)

> Nota: questa è una **soluzione di riferimento commentata** da adattare alla codebase reale (HotChocolate/Apollo equivalenti).

### 1) Persisted queries obbligatorie in produzione

Obiettivo: bloccare query ad-hoc in production per ridurre abuso e migliorare caching.

```csharp
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .UsePersistedQueryPipeline()
    .AddReadOnlyFileSystemQueryStorage("./persisted-queries");

// Middleware custom: in PROD rifiuta richieste senza hash persisted query
app.Use(async (ctx, next) =>
{
    var isGraphQl = ctx.Request.Path.StartsWithSegments("/graphql");
    var env = ctx.RequestServices.GetRequiredService<IHostEnvironment>();

    if (isGraphQl && env.IsProduction())
    {
        var hasPersistedHash = ctx.Request.Query.ContainsKey("extensions")
            || ctx.Request.Headers.ContainsKey("X-GraphQL-Document-Id");

        if (!hasPersistedHash)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Persisted query obbligatoria in produzione");
            return;
        }
    }

    await next();
});
```

**Commento:** persisted query = superficie d’attacco minore + hit-rate cache più alta.

---

### 2) Query cost analysis con limiti dinamici per tenant

```csharp
public sealed class TenantCostRule
{
    public string TenantId { get; init; } = default!;
    public int MaxDepth { get; init; }
    public int MaxComplexity { get; init; }
}

public interface ICostPolicyProvider
{
    Task<TenantCostRule> GetRuleAsync(string tenantId, CancellationToken ct);
}
```

```csharp
// Pseudo middleware GraphQL che valuta costo prima di eseguire resolver
public sealed class GraphQlCostGuardMiddleware
{
    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        var tenant = ctx.RequestServices.GetRequiredService<ITenantContextAccessor>().Current!;
        var provider = ctx.RequestServices.GetRequiredService<ICostPolicyProvider>();
        var rule = await provider.GetRuleAsync(tenant.TenantId, ctx.RequestAborted);

        var queryCost = EstimateGraphQlComplexity(ctx); // profondità + moltiplicatori connessioni

        if (queryCost.Depth > rule.MaxDepth || queryCost.Complexity > rule.MaxComplexity)
        {
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await ctx.Response.WriteAsync("Query GraphQL oltre budget costo");
            return;
        }

        await next(ctx);
    }
}
```

**Commento:** limite costo per tenant evita noisy-neighbor e difende contro query “explosive”.

---

### 3) Eliminare N+1 con DataLoader

```csharp
public sealed class ProductByIdDataLoader : BatchDataLoader<Guid, Product>
{
    private readonly AppDbContext _db;

    public ProductByIdDataLoader(
        IBatchScheduler scheduler,
        AppDbContext db,
        DataLoaderOptions? options = null)
        : base(scheduler, options)
    {
        _db = db;
    }

    protected override async Task<IReadOnlyDictionary<Guid, Product>> LoadBatchAsync(
        IReadOnlyList<Guid> keys,
        CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .Where(p => keys.Contains(p.Id))
            .ToListAsync(cancellationToken);

        return products.ToDictionary(p => p.Id);
    }
}
```

```csharp
public async Task<Product?> GetProductAsync(
    [Parent] OrderLine line,
    ProductByIdDataLoader loader,
    CancellationToken ct)
    => await loader.LoadAsync(line.ProductId, ct);
```

**Commento:** DataLoader batcha richieste per request e taglia drasticamente roundtrip DB.

---

### 4) Caching selettivo + chiavi tenant-aware

Strategia:
- cache su query ad alta lettura e bassa variabilità;
- TTL corto su dati caldi (30-120s);
- chiave cache = `tenant + scope + operationName + variablesHash`.

```csharp
public static string BuildGraphQlCacheKey(
    string tenantId,
    string scope,
    string operationName,
    string variablesHash)
    => $"gql:{tenantId}:{scope}:{operationName}:{variablesHash}";
```

**Commento:** mai usare chiavi globali senza tenant/scope in ambiente multi-tenant.

---

### 5) Invalidazione cache event-driven

```csharp
public sealed record ProductUpdatedEvent(Guid ProductId, string TenantId);

public sealed class ProductUpdatedCacheInvalidator : IEventHandler<ProductUpdatedEvent>
{
    private readonly IDistributedCache _cache;

    public async Task Handle(ProductUpdatedEvent evt, CancellationToken ct)
    {
        // pattern-based invalidation: dipende dal provider cache scelto
        // esempio concettuale: rimuovi tutte le entry che dipendono dal prodotto aggiornato
        await InvalidateByTagAsync($"tenant:{evt.TenantId}:product:{evt.ProductId}", ct);
    }
}
```

**Commento:** invalidazione guidata da eventi è più affidabile del “reset globale cache”.

---

### 6) Federation + versioning schema

Linee guida:
- ogni subgraph versione semantica del proprio schema;
- deprecazioni con finestra minima definita (es. 90 giorni);
- changelog schema e report impatto client prima del merge;
- blocco CI se breaking change non autorizzata.

Esempio SDL (deprecazione):

```graphql
type Product {
  id: ID!
  name: String!
  sku: String @deprecated(reason: "Use `code` instead")
  code: String!
}
```

**Commento:** federazione senza governance schema porta rapidamente a regressioni client.

---

## Piano test (obbligatorio)

### Test 1 — Persisted query enforcement
1. Request GraphQL con query testuale raw in production mode.
2. Atteso `400`.
3. Request con hash persisted query registrato -> `200`.

### Test 2 — Cost guard
1. Query con profondità/costo oltre soglia tenant.
2. Atteso `429` + log motivo blocco.

### Test 3 — N+1 regression
1. Esegui query lista ordini con prodotti correlati (dataset grande).
2. Confronta numero query DB prima/dopo DataLoader.

### Test 4 — Cache correctness concorrente
1. Warm cache con query prodotto.
2. Aggiorna prodotto e pubblica evento.
3. Verifica invalidazione e dato aggiornato in lettura successiva.

### Test 5 — Schema compatibility
1. Introduci campo deprecato e nuovo campo sostitutivo.
2. Verifica che client legacy continuino a funzionare durante finestra di deprecazione.

---

## Metriche minime da dashboard
- `graphql.request.count`
- `graphql.latency.p50/p95/p99`
- `graphql.rejected.cost.count`
- `graphql.persisted_query.hit_rate`
- `graphql.cache.hit_rate`
- `graphql.db.query.count.per_request`
- `graphql.schema.breaking_change.detected`

**Commento:** senza metriche su costo/rifiuti non puoi calibrare limiti tenant in modo affidabile.

---

## Deliverable richiesti
- Pipeline GraphQL con persisted queries obbligatorie in produzione.
- Cost control per tenant con policy configurabile.
- Resolver principali migrati a DataLoader.
- Caching selettivo tenant-aware + invalidazione event-driven.
- Governance schema federato con checklist CI e report compatibilità.
