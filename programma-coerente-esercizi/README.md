# Programma coerente unico (esercizi 01-08)

Questa cartella contiene un **programma funzionante unico** che integra in modo coerente tutti i pattern mostrati nelle esercitazioni:

- `01` multi-tenant resolution + isolamento dati
- `02` CQRS/Outbox/Inbox idempotente
- `03` auth/authz hardening (revoca token + policy)
- `04` GraphQL persisted query + cost guard + cache tenant-aware
- `05` observability (metriche p95 e contatori)
- `06` performance benchmark semplificato
- `07` migrazione zero-downtime (backfill + quality check)
- `08` resilience (retry + circuit breaker + fallback)

Il codice è fortemente commentato in `main.py` per spiegare il funzionamento passo-passo.

## Esecuzione demo

```bash
python3 programma-coerente-esercizi/main.py
```

## Test automatici

```bash
python3 -m unittest programma-coerente-esercizi/test_main.py
```

## Output atteso demo

Il programma stampa una sequenza `[01]..[08]` che conferma il comportamento di ogni sezione e termina con:

```text
=== Demo completata con successo ===
```
