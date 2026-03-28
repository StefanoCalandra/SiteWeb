# 03 — Hardening di Autenticazione e Autorizzazione

## Scenario
L'API deve rispettare standard elevati per clienti enterprise e audit compliance.

## Obiettivo
Rafforzare sicurezza applicativa su identità, autorizzazione e difesa in profondità.

---

## Requisiti vincolanti
- Implementare rotating signing keys (JWKS) e gestione key rollover.
- Policy-based authorization con requisiti contestuali (tenant, ruolo, rischio).
- Rate limit e adaptive throttling per endpoint sensibili.
- Rilevamento token replay e revoca near-real-time.
- Hardening header HTTP + CORS least-privilege.

## Criteri di accettazione
- Threat model STRIDE aggiornato e mitigazioni tracciate.
- Pentest checklist automatizzata in pipeline.
- Test negativi su privilege escalation e broken access control.
- Logging sicurezza con correlazione utente/sessione/request.

## Extra hard mode
- Introduci step-up authentication per operazioni ad alto rischio.
- Aggiungi motore ABAC con policy dichiarative.

---

## Svolgimento guidato (con commenti)

> Nota: questa è una **soluzione di riferimento commentata** da adattare alla codebase reale.

### 1) JWT robusto con JWKS e key rotation

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://id.example.com"; // OIDC provider
        options.Audience = "siteweb-api";

        // Usa JWKS discovery: il provider pubblica le chiavi attive
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30) // skew ridotto (default spesso troppo permissivo)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                // Hook per controlli custom: revoca, tenant mismatch, replay
                var tokenId = ctx.Principal?.FindFirst("jti")?.Value;
                var userId = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var guard = ctx.HttpContext.RequestServices.GetRequiredService<ITokenGuardService>();
                var isRevoked = await guard.IsRevokedAsync(tokenId, userId, ctx.HttpContext.RequestAborted);

                if (isRevoked)
                    ctx.Fail("Token revocato");
            }
        };
    });
```

**Commento:** key rotation significa accettare più `kid` in finestra di transizione, non invalidare tutto al rollover.

---

### 2) Authorization policy-based con contesto tenant/rischio

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageBilling", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin", "BillingManager");
        policy.RequireClaim("scope", "billing:write");
        policy.Requirements.Add(new TenantMembershipRequirement());
        policy.Requirements.Add(new RiskLevelRequirement(maxAllowedRisk: "medium"));
    });
});

builder.Services.AddScoped<IAuthorizationHandler, TenantMembershipHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RiskLevelHandler>();
```

```csharp
public sealed class TenantMembershipHandler : AuthorizationHandler<TenantMembershipRequirement>
{
    private readonly ITenantContextAccessor _tenant;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantMembershipRequirement requirement)
    {
        var tenantInToken = context.User.FindFirst("tenant_id")?.Value;
        var requestTenant = _tenant.Current?.TenantId;

        if (!string.IsNullOrWhiteSpace(tenantInToken) && tenantInToken == requestTenant)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

**Commento:** evita policy solo su ruolo; ruolo senza contesto tenant è una causa frequente di broken access control.

---

### 3) Rate limiting + adaptive throttling

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("SensitiveEndpoints", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: $"{httpContext.User.Identity?.Name ?? "anon"}:{httpContext.Connection.RemoteIpAddress}",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                TokensPerPeriod = 20,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
```

```csharp
app.MapPost("/api/auth/rotate-keys", RotateKeys)
   .RequireAuthorization("CanManageBilling")
   .RequireRateLimiting("SensitiveEndpoints");
```

**Commento:** per endpoint critici combina rate-limit + controllo rischio + logging sicurezza.

---

### 4) Token replay detection e revoca near-real-time

Strategia consigliata:
- salva `jti` token in cache distribuita all’autenticazione iniziale;
- marca `jti` come revocato in logout/password reset/incident response;
- controlla revoca in `OnTokenValidated`;
- opzionale: blocca riuso stesso `jti` da fingerprint sospetti.

```csharp
public interface ITokenGuardService
{
    Task<bool> IsRevokedAsync(string? jti, string? userId, CancellationToken ct);
    Task RevokeAsync(string jti, string userId, DateTime expiresUtc, CancellationToken ct);
}
```

**Commento:** su incidenti reali la revoca near-real-time è cruciale per ridurre il blast radius.

---

### 5) Hardening HTTP headers + CORS least-privilege

Checklist minima:
- `Strict-Transport-Security` (solo HTTPS);
- `X-Content-Type-Options: nosniff`;
- `X-Frame-Options: DENY`;
- `Content-Security-Policy` (se serve front-end integrato);
- CORS con allowlist esplicita di origin, metodi, header.

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        policy.WithOrigins("https://app.customer-a.com", "https://portal.customer-b.com")
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});
```

**Commento:** evita `AllowAnyOrigin/AllowAnyHeader` in produzione su API con token bearer.

---

### 6) Logging sicurezza e correlazione

Campi minimi in log sicurezza:
- `timestamp_utc`
- `tenant_id`
- `user_id`
- `session_id`
- `request_id` / `correlation_id`
- `action`
- `resource`
- `decision` (`allow` / `deny`)
- `reason`
- `source_ip`

**Commento:** log senza contesto identità/tenant serve poco in audit e forensics.

---

## Piano test (obbligatorio)

### Test 1 — Broken access control
1. Utente `Viewer` tenta endpoint admin.
2. Atteso `403`, mai `200`.

### Test 2 — Tenant boundary
1. Token tenant `A` invoca risorsa tenant `B`.
2. Atteso `403`/`404` secondo policy di masking.

### Test 3 — Token revoca
1. Emissione token valido.
2. Revoca `jti` lato server.
3. Nuova richiesta con stesso token deve fallire (`401`).

### Test 4 — Replay
1. Invia stesso token da due contesti sospetti (IP/device fingerprint diversi).
2. Verifica alert + blocco (se policy attiva).

### Test 5 — Rate limit
1. Burst oltre soglia su endpoint sensibile.
2. Atteso `429` con headers di retry.

### Test 6 — Header/CORS
1. Richiesta da origin non in allowlist.
2. Preflight deve fallire.

---

## Threat model (STRIDE) minimo
- **S**poofing: token theft, session hijack.
- **T**ampering: alterazione token/payload.
- **R**epudiation: log incompleti/non firmati.
- **I**nformation disclosure: scope eccessivi, CORS aperto.
- **D**enial of service: assenza rate limiting.
- **E**levation of privilege: policy deboli solo role-based.

**Commento:** collega ogni minaccia a mitigazione concreta e test automatizzato.

---

## Deliverable richiesti
- Configurazione authn/authz hardenizzata con policy contestuali.
- Implementazione revoca + replay detection.
- Rate limiting su endpoint critici.
- Test negativi sicurezza in pipeline CI.
- Documento operativo con runbook incident response identità.
