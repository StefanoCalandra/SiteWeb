# 06 — Performance Engineering, Benchmarking e Capacity Planning

## Scenario
Il traffico previsto triplica nei prossimi 6 mesi e serve un piano tecnico basato su misure.

## Obiettivo
Definire e implementare una strategia completa di performance engineering e capacity planning.

---

## Requisiti vincolanti
- Benchmark sintetici e realistici (load + stress + soak).
- Profiling CPU/memoria/GC con analisi hotspot.
- Ottimizzazione query DB (indici, piani esecuzione, batching).
- Modello di capacity forecasting con margini di sicurezza.
- Definizione di performance budget per feature nuove.

## Criteri di accettazione
- Riduzione p95 e p99 con target espliciti.
- Stabilità sotto soak test prolungato (no memory leak).
- Documento capacity con scenari best/expected/worst case.
- Gate CI su regressioni performance oltre soglia.

## Extra hard mode
- Ottimizza cold-start e autoscaling con metriche custom.
- Introduci workload replay da traffico anonimizzato reale.

---

## Svolgimento guidato (con commenti)

### 1) Definire workload model realistico

Prima di testare, descrivi il profilo traffico:
- endpoint top (`/orders`, `/catalog`, `/checkout`);
- mix richieste read/write (es. 80/20);
- distribuzione tenant (top 10 tenant + long tail);
- pattern temporale (picchi orari/giornalieri);
- payload medi e payload worst-case.

**Commento:** senza workload model, i benchmark rischiano di essere “bench da laboratorio”.

---

### 2) Baseline prima di ottimizzare

Checklist baseline:
- p50/p95/p99 attuali per endpoint critici;
- throughput sostenibile (RPS) entro SLO;
- CPU/RAM per istanza;
- query DB più costose;
- error rate e timeout rate.

Template tabella baseline:
- endpoint
- RPS
- p95
- p99
- error_rate
- cpu_avg
- mem_avg

**Commento:** ogni intervento deve essere confrontato contro baseline, non contro percezioni.

---

### 3) Load, stress, soak (con obiettivi chiari)

- **Load test**: traffico atteso (es. 500 RPS per 30 min).
- **Stress test**: oltre capacità fino al punto di rottura controllato.
- **Soak test**: carico stabile 8-24h per trovare leak/degrado lento.

Esempio target:
- p95 < 250ms a 500 RPS
- p99 < 600ms a 500 RPS
- error rate < 0.5%
- nessuna crescita memoria > 10% in soak 8h

**Commento:** la qualità del test dipende da scenari, non solo dal tool scelto.

---

### 4) Esempio script load test (k6)

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: {
    steady_load: {
      executor: 'constant-arrival-rate',
      rate: 500,
      timeUnit: '1s',
      duration: '30m',
      preAllocatedVUs: 200,
      maxVUs: 800,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.005'],
    http_req_duration: ['p(95)<250', 'p(99)<600'],
  },
};

export default function () {
  const res = http.get(`${__ENV.BASE_URL}/api/orders?limit=20`);
  check(res, {
    'status is 200': r => r.status === 200,
  });
  sleep(0.1);
}
```

**Commento:** usa dataset realistico e warmup iniziale, altrimenti i numeri sono fuorvianti.

---

### 5) Profiling applicativo (CPU, memory, GC)

Aree da profilare:
- CPU hotspot (serializzazione, mapping, LINQ costosi);
- allocazioni memory in path caldo;
- GC pause e pressure generation;
- lock contention/thread pool starvation.

Esempio misura custom con `Meter`:

```csharp
public static class PerfMetrics
{
    public static readonly Meter Meter = new("SiteWeb.Api.Perf");
    public static readonly Histogram<double> EndpointLatencyMs =
        Meter.CreateHistogram<double>("api.endpoint.latency.ms");
}
```

**Commento:** prima elimina i colli di bottiglia dominanti; micro-ottimizzazioni dopo.

---

### 6) Ottimizzazione DB guidata da dati

Checklist DB:
- `EXPLAIN/ANALYZE` sulle query top;
- indici coerenti con filtri/sort;
- proiezioni mirate (evita SELECT *);
- paging efficiente (`keyset pagination` dove possibile);
- batching scritture;
- prevenzione N+1.

Pattern query projection:

```csharp
var result = await _db.Orders
    .Where(x => x.TenantId == tenantId)
    .OrderByDescending(x => x.CreatedUtc)
    .Select(x => new OrderListItemDto(x.Id, x.Status, x.Total))
    .Take(50)
    .ToListAsync(ct);
```

**Commento:** ottimizzare query riduce sia latenza sia costo infrastrutturale.

---

### 7) Capacity planning e forecasting

Formula base:
- capacità istanza = RPS entro SLO;
- capacità cluster = capacità istanza * istanze attive;
- capacità effettiva = capacità cluster * (1 - margine sicurezza).

Margine consigliato:
- 30-40% per picchi/failure.

Scenari obbligatori:
- **Best case**: traffico sotto previsione.
- **Expected case**: stima nominale.
- **Worst case**: picco + perdita 1 zona/nodo.

**Commento:** capacity plan deve includere failure scenario, non solo traffico medio.

---

### 8) Performance budget per feature e gate CI

Budget per ogni feature:
- latenza max aggiuntiva (ms);
- query DB aggiuntive;
- allocazioni memory addizionali;
- chiamate remote extra.

Esempio regola gate:
- blocca merge se p95 peggiora >10% vs baseline;
- blocca merge se error rate >0.5% nel test standard.

```bash
# esempio concettuale in CI
./perf/run-benchmark.sh --scenario=standard --compare-baseline=perf/baseline.json --max-regression-p95=10
```

**Commento:** senza gate automatico le regressioni rientrano in produzione silenziosamente.

---

### 9) Runbook performance incident

Sezione minima:
1. Sintomi (SLI in violazione).
2. Comandi/metriche da controllare per primi.
3. Decision tree (DB bottleneck? CPU saturation? downstream slow?).
4. Mitigazioni immediate (scale out, rollback, disable feature flag).
5. Criterio di chiusura incidente.

**Commento:** runbook riduce MTTD/MTTR e dipendenza da conoscenza tacita.

---

## Piano test (obbligatorio)

### Test 1 — Baseline reproducibility
1. Esegui benchmark su ambiente stabile.
2. Ripeti 3 volte.
3. Verifica varianza accettabile (<5-10%).

### Test 2 — Load target
1. Carico nominale 500 RPS per 30m.
2. Verifica p95/p99 e error rate entro target.

### Test 3 — Stress behavior
1. Incrementa RPS fino al limite.
2. Verifica degradazione controllata (no crash a cascata).

### Test 4 — Soak leak detection
1. Carico costante 8h.
2. Verifica stabilità memoria, GC pause e error spike.

### Test 5 — DB hotspot
1. Profila query top durante load test.
2. Applica ottimizzazione.
3. Confronta prima/dopo con metriche.

### Test 6 — Regression gate
1. Esegui benchmark pre/post modifica.
2. Blocca merge se soglia regressione superata.

---

## Metriche minime da dashboard
- `api.latency.p50/p95/p99`
- `api.rps`
- `api.error.rate`
- `cpu.utilization`
- `memory.working_set`
- `gc.pause.ms`
- `threadpool.queue.length`
- `db.query.duration.p95`
- `db.connections.in_use`

---

## Deliverable richiesti
- Workload model documentato.
- Report benchmark (baseline + miglioramenti).
- Lista ottimizzazioni con impatto misurato.
- Capacity plan best/expected/worst con margini.
- Gate regressione performance in CI/CD.
- Runbook performance incident operativo.
