# 08 — Reliability e Resilience Patterns Avanzati

## Scenario
Le dipendenze esterne sono instabili e causano degrado a cascata.

## Obiettivo
Rendere il sistema anti-fragile usando pattern di resilienza evoluti.

---

## Requisiti vincolanti
- Circuit breaker per dipendenza con soglie dinamiche.
- Timeout budget per catena chiamate distribuite.
- Bulkhead isolation per pool critici.
- Fallback semantici e degraded mode esplicito.
- Hedged requests solo dove il trade-off è favorevole.

## Criteri di accettazione
- Test di resilienza con fault injection automatizzato.
- Riduzione error burst e recovery time misurata.
- Nessun resource exhaustion in scenari di picco + fault.
- Documento decisionale con trade-off e limiti noti.

## Extra hard mode
- Adaptive concurrency limits basati su latenza osservata.
- Simulazione game day multi-team con KPI di esito.

---

## Svolgimento guidato (con commenti)

### 1) Dependency criticality matrix (prima dei pattern)

Classifica ogni dipendenza:
- criticità business (alta/media/bassa);
- timeout tollerabile;
- idempotenza (sì/no);
- fallback disponibile (sì/no);
- retry consentito (sì/no).

Esempio:
- `payments`: criticità alta, timeout 800ms, retry limitato, no hedging.
- `catalog-read`: criticità media, timeout 300ms, hedging possibile.

**Commento:** senza classificazione iniziale applichi pattern sbagliati al posto sbagliato.

---

### 2) Timeout budget per catena chiamate

Regola:
- timeout totale request = 2s;
- budget per dipendenza (payment 800ms, inventory 500ms, shipping 500ms);
- margine orchestrazione interna ~200ms.

```csharp
public static class TimeoutBudgets
{
    public static readonly TimeSpan Payment = TimeSpan.FromMilliseconds(800);
    public static readonly TimeSpan Inventory = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan Shipping = TimeSpan.FromMilliseconds(500);
}
```

**Commento:** timeout troppo lunghi saturano thread/socket e peggiorano il tail latency.

---

### 3) Pipeline resilienza per dipendenza (Polly)

```csharp
var paymentPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddTimeout(TimeoutBudgets.Payment)
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 2,
        Delay = TimeSpan.FromMilliseconds(120),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is HttpRequestException)
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 20,
        BreakDuration = TimeSpan.FromSeconds(20)
    })
    .Build();
```

**Commento:** ordine tipico: timeout -> retry -> circuit breaker (valida sul tuo caso d’uso).

---

### 4) Bulkhead isolation per evitare cascade failure

```csharp
var bulkhead = new SemaphoreSlim(initialCount: 50, maxCount: 50); // limite concorrenza dipendenza

public async Task<T> ExecuteWithBulkheadAsync<T>(Func<Task<T>> work)
{
    if (!await bulkhead.WaitAsync(TimeSpan.FromMilliseconds(50)))
        throw new BulkheadRejectedException("Bulkhead saturo");

    try
    {
        return await work();
    }
    finally
    {
        bulkhead.Release();
    }
}
```

Strategia:
- pool separati per dipendenze critiche;
- limiti diversi per read/write;
- queue limitata per evitare accumulo infinito.

**Commento:** bulkhead trasforma fail “totale” in fail “contenuto”.

---

### 5) Retry policy sicure (anti retry-storm)

Regole:
- retry solo su errori transienti (timeouts, 5xx, network);
- max tentativi 2-3;
- exponential backoff + jitter;
- no retry su 4xx business.

Checklist anti-storm:
- cap globale retry/sec;
- metriche su retry amplification;
- disattivazione rapida via config flag.

**Commento:** retry non governato aumenta il carico proprio quando il sistema è fragile.

---

### 6) Fallback semantico e degraded mode esplicito

Pattern esempi:
- `pricing down` -> usa ultimo prezzo valido con label “stima”; 
- `recommendation down` -> risposta senza suggerimenti;
- `payment score down` -> workflow manual-review.

```csharp
public async Task<PriceDto> GetPriceAsync(string sku, CancellationToken ct)
{
    try
    {
        return await _pricingClient.GetLivePriceAsync(sku, ct);
    }
    catch
    {
        var cached = await _priceCache.GetLastKnownAsync(sku, ct);
        return new PriceDto(cached.Value, source: "fallback-cache", isDegraded: true);
    }
}
```

**Commento:** fallback deve essere tracciabile (`isDegraded=true`) per osservabilità e UX corretta.

---

### 7) Hedged requests (solo read idempotenti)

Quando usarle:
- endpoint read-only;
- elevata tail latency (p99 alta);
- backend con headroom sufficiente.

Quando evitarle:
- endpoint write/side effect;
- backend già saturo.

Esempio logico:
- invia richiesta primaria;
- dopo 80ms, se non risposta, invia hedge secondaria;
- usa la prima risposta valida e cancella l’altra.

**Commento:** hedging migliora p99 ma aumenta QPS; monitorare sempre costo extra.

---

### 8) Adaptive concurrency limits

Strategia:
- misura EWMA latenza;
- se latenza supera soglia, riduci limite concorrenza;
- in recovery, rialza gradualmente;
- imposta floor/ceiling per evitare oscillazioni.

**Commento:** è una difesa proattiva contro overload, prima che scatti incidente grave.

---

### 9) Game day e runbook resilienza

Game day trimestrale:
- fault su dipendenza critica;
- verifica runbook, allerta e comunicazione cross-team;
- KPI: MTTD, MTTR, error burst peak, user impact.

Runbook minimo:
1. Identifica dipendenza degradata.
2. Abilita modalità degradata prevista.
3. Riduci pressione (throttle/feature flag).
4. Verifica KPI stabilizzazione.
5. Chiusura e postmortem.

**Commento:** resilienza senza esercitazione periodica tende a degradare nel tempo.

---

## Piano test (obbligatorio)

### Test 1 — Circuit breaker lifecycle
1. Simula failure ratio >50%.
2. Verifica transizione closed -> open -> half-open -> closed.

### Test 2 — Timeout budget enforcement
1. Aumenta latenza dipendenza oltre budget.
2. Verifica timeout rapido e assenza saturazione thread pool.

### Test 3 — Bulkhead containment
1. Saturare dipendenza non critica.
2. Verifica che endpoint critici restino disponibili.

### Test 4 — Retry storm prevention
1. Introduci fault transiente ad alta frequenza.
2. Verifica cap retry e assenza amplificazione eccessiva.

### Test 5 — Degraded mode correctness
1. Spegni servizio pricing.
2. Verifica fallback coerente e campo `isDegraded=true`.

### Test 6 — Hedging trade-off
1. Abilita hedging su endpoint read-only.
2. Misura riduzione p99 e aumento QPS backend.

### Test 7 — Adaptive concurrency
1. Simula latenza crescente.
2. Verifica riduzione automatica concurrency limit e recovery progressiva.

---

## Metriche minime da dashboard
- `dependency.timeout.count`
- `circuit_breaker.open.count`
- `bulkhead.rejected.count`
- `retry.attempt.count`
- `retry.amplification.factor`
- `fallback.activation.count`
- `degraded_mode.active`
- `hedge.request.count`
- `latency.p95/p99`

---

## Deliverable richiesti
- Dependency criticality matrix per servizi esterni.
- Policy resilience per ciascuna dipendenza critica.
- Config timeout/retry/circuit-breaker/bulkhead con rationale.
- Degraded mode documentato e testato.
- Report fault injection + game day con KPI.
- ADR finale con trade-off e limiti noti.
