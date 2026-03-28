# 05 — Observability, SRE e Chaos Engineering

## Scenario
Il sistema deve offrire affidabilità elevata e diagnosi rapida durante incidenti.

## Obiettivo
Costruire osservabilità completa (logs, metrics, traces) e validare resilienza con chaos testing.

---

## Requisiti vincolanti
- OpenTelemetry end-to-end con propagazione trace context.
- SLI/SLO formalizzati per latenza, errori, disponibilità.
- Alerting actionabile con runbook e ownership.
- Chaos experiment periodici su DB, broker e dipendenze esterne.
- Error budget policy con gate su release.

## Criteri di accettazione
- Incident drill con MTTR ridotto rispetto baseline.
- Dashboards orientate a golden signals.
- Postmortem blameless con azioni preventive tracciate.
- Copertura tracing >90% delle rotte business critiche.

## Extra hard mode
- A/B test di strategie retry/circuit breaker con analisi comparativa.
- Introduci auto-remediation controllata su alert frequenti.

---

## Svolgimento guidato (con commenti)

### 1) OpenTelemetry end-to-end (traces + metrics + logs)

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "SiteWeb.Api", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("SiteWeb.Business")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddOtlpExporter());
```

**Commento:** escludere endpoint rumorosi (es. `/health`) aiuta a ridurre cardinalità inutile.

---

### 2) Correlation ID e structured logging obbligatori

```csharp
app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N");

    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    ctx.Items["CorrelationId"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```

Campi minimi log applicativi:
- `timestamp_utc`
- `level`
- `service`
- `tenant_id`
- `user_id` (se disponibile)
- `correlation_id`
- `trace_id`
- `span_id`
- `event_name`
- `error_code`

**Commento:** senza correlazione uniforme, incident response perde minuti preziosi.

---

### 3) Definizione SLI/SLO e burn-rate

SLI minimi:
- Availability API = richieste `2xx/3xx` su totale.
- Latency API = p95 endpoint critici.
- Freshness eventi = tempo tra evento generato e consumato.

SLO esempio (finestra 30 giorni):
- Availability: **99.9%**
- p95 `/orders/*`: **< 250ms**
- Event lag outbox: **< 60s**

Burn-rate policy:
- burn-rate > 2x (5 minuti): alert warning.
- burn-rate > 6x (15 minuti): alert critical + incident.

**Commento:** la combinazione finestre corte + lunghe riduce falsi positivi e ritardi rilevazione.

---

### 4) Error budget policy operativa

Regole consigliate:
- consumo budget < 25%: delivery normale;
- 25-50%: review preventiva delle release;
- 50-100%: freeze feature non critiche;
- >100%: solo reliability work fino a recovery.

Checklist review quando budget alto:
- regressioni recenti post deploy;
- endpoint/tenant maggiormente impattati;
- azioni di mitigazione entro 24h.

**Commento:** il budget deve guidare priorità prodotto/ingegneria, non solo monitoraggio.

---

### 5) Alerting actionabile con runbook

Template alert:
- Nome: `api-latency-p95-breach`
- Condizione: p95 > 250ms per 10m
- Severità: High
- Owner: Team API
- Runbook: link obbligatorio
- Escalation: on-call primaria -> secondaria -> incident commander

Runbook minimo:
1. Verifica impatto utente (errori, latenza, tenant colpiti).
2. Controlla ultimi deploy/config changes.
3. Isola componente sospetto (DB, cache, broker, downstream).
4. Applica mitigazione standard (scale out, rollback, feature flag).
5. Conferma recovery su SLI per 15m.

**Commento:** un alert senza runbook è lavoro incompleto.

---

### 6) Dashboard golden signals + business signals

Pannelli minimi:
- **Latency** (p50/p95/p99)
- **Traffic** (RPS, endpoint top)
- **Errors** (5xx, timeout, auth failures)
- **Saturation** (CPU, memory, thread pool, connessioni DB)
- **Business KPI** (ordini completati/min, conversion rate)

Aggiunte utili:
- marker deploy/release;
- segmentazione per tenant;
- top error fingerprints.

**Commento:** dashboard tecnica senza KPI business rende difficile priorizzare incidenti.

---

### 7) Chaos engineering controllato (hypothesis-driven)

Formato esperimento:
- **Ipotesi**: “Con +500ms DB latency, p95 resta < 400ms grazie a cache + fallback”.
- **Blocco test**: staging/prod canary.
- **Blast radius**: max 5% traffico.
- **Stop condition**: error rate > 2% per 3m.
- **Learning outcome**: decisioni concrete su config/codice/runbook.

Esempi:
1. Latency injection +500ms su DB per 5 minuti.
2. Drop 20% chiamate verso servizio pagamenti.
3. Kill pod random del dispatcher outbox.

**Commento:** chaos utile solo se produce azioni migliorative tracciabili.

---

### 8) Incident drill e postmortem blameless

Incident drill (mensile):
- simula incidente realistico;
- misura MTTD, MTTR, qualità handoff on-call;
- verifica runbook e alert coverage.

Template postmortem:
- impatto (utenti/tenant/ricavi)
- timeline UTC
- root cause tecnica + sistemica
- detection gaps
- azioni correttive (owner + data)
- verifica efficacia entro sprint successivo

**Commento:** la qualità del postmortem predice miglioramento reale del sistema.

---

## Piano test (obbligatorio)

### Test 1 — Tracing coverage
1. Invoca 10 endpoint core.
2. Verifica trace complete (API -> DB -> downstream).
3. Target: copertura >90% rotte critiche.

### Test 2 — Correlation/log quality
1. Genera errore su endpoint business.
2. Verifica presenza `correlation_id`, `trace_id`, `tenant_id` nei log.

### Test 3 — Burn-rate alert
1. Simula aumento errori oltre soglia.
2. Verifica trigger warning/critical su finestre multiple.

### Test 4 — Chaos DB latency
1. Introduci latency DB controllata.
2. Verifica impatto e rispetto stop condition.
3. Registra outcome e azione migliorativa.

### Test 5 — Error budget gate
1. Porta budget oltre 50%.
2. Verifica freeze feature e priorità reliability.

### Test 6 — Incident drill
1. Esegui simulazione end-to-end.
2. Misura MTTD/MTTR e confronta con baseline.

---

## Metriche minime da dashboard
- `http.server.duration` (p50/p95/p99)
- `http.server.request.count`
- `http.server.error.rate`
- `db.client.duration`
- `outbox.lag.seconds`
- `slo.burn_rate`
- `incident.mttd.minutes`
- `incident.mttr.minutes`

---

## Deliverable richiesti
- Telemetria OTel completa (logs/metrics/traces).
- Catalogo SLI/SLO con ownership.
- Alert rulebook + runbook associati.
- Piano chaos trimestrale con esperimenti hypothesis-driven.
- Processo incident drill + postmortem blameless operativo.
