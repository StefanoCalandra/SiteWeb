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

### 1) OpenTelemetry end-to-end

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("SiteWeb.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("SiteWeb.Business")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());
```

**Commento:** tracing senza propagazione `traceparent` su chiamate esterne spezza la visibilità end-to-end.

---

### 2) Definizione SLI/SLO e error budget

SLI minimi:
- Availability API = richieste `2xx/3xx` su totale.
- Latency API = p95 endpoint critici.
- Freshness eventi = tempo tra evento generato e consumato.

SLO esempio (30 giorni):
- Availability: **99.9%**
- p95 `/orders/*`: **< 250ms**
- Event lag outbox: **< 60s**

Error budget:
- Budget mensile = `100% - SLO`.
- Se budget consumato > 50%, freeze feature non critiche.
- Se budget consumato > 100%, solo lavori di affidabilità.

**Commento:** SLO senza policy di conseguenza resta un KPI “decorativo”.

---

### 3) Alerting actionabile

Regole:
- un alert = una azione chiara;
- niente alert “rumorosi” senza owner;
- severità legata all’impatto utente.

Template alert:
- Nome: `api-latency-p95-breach`
- Condizione: p95 > 250ms per 10m
- Owner: Team API
- Runbook: link procedura diagnosi
- Escalation: on-call primaria -> secondaria

**Commento:** riduci alert fatigue con tuning continuo e deduplicazione.

---

### 4) Dashboard golden signals

Pannelli minimi:
- **Latency** (p50/p95/p99)
- **Traffic** (RPS, endpoint top)
- **Errors** (5xx rate, error classes)
- **Saturation** (CPU, memory, thread pool, connessioni DB)
- **Business KPI** (ordini completati/min)

**Commento:** aggiungi sempre correlazione deploy-marker per vedere regressioni dopo release.

---

### 5) Chaos engineering controllato

Esempi esperimenti:
1. Latency injection +500ms su DB per 5 minuti.
2. Drop 20% chiamate verso servizio pagamenti.
3. Kill pod random del dispatcher outbox.

Guardrail:
- ambiente staging o prod canary controllato;
- blast radius limitato;
- criterio di stop immediato;
- finestra temporale concordata on-call.

**Commento:** chaos senza ipotesi misurabile è solo “rottura”, non apprendimento.

---

### 6) Postmortem blameless

Struttura minima:
- Timeline UTC evento/incidente.
- Root cause tecnica e sistemica.
- Cosa ha funzionato / non funzionato.
- Azioni correttive con owner e data.
- Follow-up verificato entro sprint successivo.

**Commento:** postmortem deve migliorare il sistema, non cercare colpevoli.

---

## Piano test (obbligatorio)

### Test 1 — Tracing coverage
1. Invoca 10 endpoint core.
2. Verifica presenza trace complete (ingresso API -> DB -> chiamata esterna).
3. Target: copertura >90% su rotte critiche.

### Test 2 — Alert drill
1. Simula breach p95 in staging.
2. Verifica alert ricevuto, runbook seguito, recupero entro obiettivo.

### Test 3 — Chaos DB latency
1. Introduci latency controllata su DB.
2. Misura impatto su p95 e error rate.
3. Verifica comportamento retry/circuit breaker.

### Test 4 — Error budget policy
1. Simula consumo budget oltre soglia.
2. Verifica attivazione gate release.

---

## Metriche minime da dashboard
- `http.server.duration` (p50/p95/p99)
- `http.server.request.count`
- `http.server.error.rate`
- `db.client.duration`
- `outbox.lag.seconds`
- `slo.burn_rate`
- `incident.mttr.minutes`

---

## Deliverable richiesti
- Telemetria OTel completa (logs/metrics/traces).
- SLI/SLO documentati e approvati.
- Alert + runbook con owner.
- Chaos plan trimestrale con risultati.
- Processo postmortem blameless operativo.
