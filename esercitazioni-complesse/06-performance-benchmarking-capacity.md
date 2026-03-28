# 06 — Performance Engineering, Benchmarking e Capacity Planning

## Scenario
Il traffico previsto triplica nei prossimi 6 mesi e serve un piano tecnico basato su misure.

## Obiettivo
Definire e implementare una strategia completa di performance engineering e capacity planning.

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
