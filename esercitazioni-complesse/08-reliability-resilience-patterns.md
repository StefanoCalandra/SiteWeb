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

### 1) Timeout budget per catena chiamate

Regola:
- timeout totale request = 2s;
- suddividi budget tra dipendenze (es. payment 800ms, inventory 500ms, shipping 500ms);
- lascia margine orchestrazione interna.

**Commento:** timeout assenti o troppo lunghi amplificano saturation e code.

---

### 2) Circuit breaker dinamico

```csharp
var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddTimeout(TimeSpan.FromMilliseconds(800))
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 20,
        BreakDuration = TimeSpan.FromSeconds(20)
    })
    .Build();
```

**Commento:** tarare soglie per dipendenza; un solo profilo globale raramente funziona.

---

### 3) Bulkhead isolation

Esempio:
- pool connessioni dedicato per servizio pagamenti;
- limite concorrenza separato per endpoint ad alto costo;
- code isolate per worker critici.

**Commento:** bulkhead previene il collasso totale quando una dipendenza degrada.

---

### 4) Retry con backoff + jitter (non ovunque)

Linee guida:
- retry solo su errori transienti;
- max tentativi contenuti (2-3);
- backoff esponenziale con jitter;
- no retry su errori business (4xx non transitori).

**Commento:** retry indiscriminati peggiorano i picchi (retry storm).

---

### 5) Fallback semantico e degraded mode

Esempi:
- catalogo in sola lettura se pricing esterno down;
- check-out limitato senza promozioni dinamiche;
- mostra stato “dato temporaneamente non aggiornato”.

**Commento:** degraded mode deve essere esplicito per utente e operazioni.

---

### 6) Hedged requests (uso selettivo)

Quando usarle:
- chiamate idempotenti read-only;
- elevata tail latency (p99 molto alta);
- capacità backend sufficiente.

Quando evitarle:
- operazioni con side effect;
- sistemi già saturi.

**Commento:** hedging riduce latenza tail ma aumenta carico complessivo.

---

### 7) Adaptive concurrency limits

Strategia:
- misura latenza recente;
- riduci concorrenza quando latenza cresce oltre soglia;
- rialza gradualmente in recovery.

**Commento:** limita overload prima che diventi incidente completo.

---

## Piano test (obbligatorio)

### Test 1 — Circuit breaker open/close
1. Simula 50% failure ratio su dipendenza.
2. Verifica apertura breaker.
3. Verifica half-open e recovery controllata.

### Test 2 — Timeout budget
1. Introduci latency crescente su chiamata esterna.
2. Verifica timeout entro budget e nessuna saturazione thread pool.

### Test 3 — Bulkhead effectiveness
1. Saturare dipendenza non critica.
2. Verifica che endpoint core restino disponibili.

### Test 4 — Retry storm prevention
1. Fault transiente ad alta frequenza.
2. Verifica numero retry totale entro limiti previsti.

### Test 5 — Degraded mode
1. Downstream servizio pricing indisponibile.
2. Verifica risposta degradata coerente e tracciata.

### Test 6 — Hedging trade-off
1. Abilita hedging su endpoint read-only.
2. Misura riduzione p99 e aumento carico backend.

---

## Metriche minime da dashboard
- `dependency.timeout.count`
- `circuit_breaker.open.count`
- `bulkhead.rejected.count`
- `retry.attempt.count`
- `fallback.activation.count`
- `degraded_mode.active`
- `latency.p95/p99`

---

## Deliverable richiesti
- Policy resilience per ciascuna dipendenza critica.
- Configurazione timeout/retry/circuit breaker/bulkhead.
- Degraded mode documentato e testato.
- Report fault injection con KPI (recovery time, error burst).
- ADR con trade-off dei pattern applicati.
