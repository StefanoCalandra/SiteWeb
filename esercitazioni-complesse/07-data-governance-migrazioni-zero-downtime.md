# 07 — Data Governance e Migrazioni Zero-Downtime

## Scenario
Sono richieste evoluzioni schema frequenti senza interrompere il servizio.

## Obiettivo
Progettare pipeline di migrazioni sicure, reversibili e compatibili con rilasci continui.

## Requisiti vincolanti
- Strategia expand/contract su schema database.
- Backfill dati incrementale con throttling e checkpoint.
- Compatibilità forward/backward tra versioni applicative.
- Data quality checks automatici prima/dopo migrazione.
- Piano rollback con RTO/RPO dichiarati.

## Criteri di accettazione
- Esecuzione migrazione in staging a carico reale.
- Zero errori bloccanti lato API durante transizione.
- Report di consistenza dati post-migrazione.
- Runbook operativo con tempi e responsabilità.

## Extra hard mode
- Migrazione online di tabella ad alto volume con shadow writes.
- Introduzione CDC per sincronizzazione graduale tra modelli.
