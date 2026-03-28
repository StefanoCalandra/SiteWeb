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

### 1) Baseline prima di ottimizzare

Checklist baseline:
- endpoint top per volume e criticità business;
- p50/p95/p99 attuali;
- throughput sostenibile;
- consumo CPU/RAM per istanza;
- query DB più costose.

**Commento:** ottimizzare senza baseline crea miglioramenti “percepiti” ma non dimostrabili.

---

### 2) Test load, stress, soak

- **Load test**: traffico atteso (es. 500 RPS per 30 min).
- **Stress test**: oltre capacità fino a degradazione.
- **Soak test**: carico stabile prolungato (6-24h).

Esempio obiettivi:
- p95 < 250ms a 500 RPS
- error rate < 0.5%
- nessun incremento memoria > 10% in soak 8h

**Commento:** soak test è fondamentale per trovare leak e degradazioni lente.

---

### 3) Profiling applicativo

Aree da profilare:
- CPU hotspot (serializzazione, mapping, LINQ costosi);
- allocazioni memory (boxing, stringhe, buffer);
- GC pause time;
- lock contention/thread pool starvation.

Azioni tipiche:
- ridurre allocazioni in path caldo;
- caching di risultati stabili;
- minimizzare reflection/runtime mapping overhead.

**Commento:** prima misura, poi ottimizza solo i colli di bottiglia reali.

---

### 4) Performance DB

Checklist DB:
- `EXPLAIN/ANALYZE` query critiche;
- indici coerenti con filtri e ordinamenti;
- evitare SELECT eccessive (proiezioni mirate);
- batching per scritture massive;
- prevenzione N+1 applicativo.

**Commento:** spesso il bottleneck è DB, non API layer.

---

### 5) Capacity planning

Formula semplificata:
- capacità istanza = RPS sostenibile entro SLO;
- capacità cluster = capacità istanza * numero istanze;
- margine operativo = 30-40% (picchi + fault).

Scenari richiesti:
- Best case (traffico inferiore stima)
- Expected case (stima centrale)
- Worst case (picchi stagionali + failure parziale nodi)

**Commento:** capacity plan senza scenario di fault è incompleto.

---

### 6) Performance budget in delivery

Per ogni nuova feature:
- budget latenza (ms)
- budget query DB
- budget allocazioni memory
- budget chiamate remote

Policy:
- PR bloccata se regression > soglia (es. +10% p95).

**Commento:** il budget evita deriva prestazionale sprint dopo sprint.

---

## Piano test (obbligatorio)

### Test 1 — Baseline reproducibility
1. Esegui benchmark su ambiente stabile.
2. Ripeti 3 volte e verifica varianza accettabile.

### Test 2 — Load target
1. Carico nominale 500 RPS.
2. Verifica p95/p99 entro target.

### Test 3 — Stress behavior
1. Incremento RPS graduale fino a failure.
2. Verifica degradazione controllata e assenza crash a cascata.

### Test 4 — Soak leak detection
1. Carico costante per 8h.
2. Verifica memoria stabile e assenza error spike.

### Test 5 — Regression gate
1. Esegui benchmark pre/post modifica.
2. Blocca merge se regressione oltre soglia.

---

## Metriche minime da dashboard
- `api.latency.p50/p95/p99`
- `api.rps`
- `api.error.rate`
- `cpu.utilization`
- `memory.working_set`
- `gc.pause.ms`
- `db.query.duration.p95`

---

## Deliverable richiesti
- Report benchmark (baseline + miglioramenti).
- Lista ottimizzazioni con impatto misurato.
- Capacity plan best/expected/worst.
- Gate regressione in CI/CD.
- Runbook performance incident.
