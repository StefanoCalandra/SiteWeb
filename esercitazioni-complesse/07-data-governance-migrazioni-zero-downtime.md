# 07 — Data Governance e Migrazioni Zero-Downtime

## Scenario
Sono richieste evoluzioni schema frequenti senza interrompere il servizio.

## Obiettivo
Progettare pipeline di migrazioni sicure, reversibili e compatibili con rilasci continui.

---

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

---

## Svolgimento guidato (con commenti)

### 1) Strategia expand/contract

Fase **expand**:
- aggiungi nuove colonne/tabelle compatibili;
- scrivi su vecchio + nuovo formato (dual write controllata);
- leggi ancora dal vecchio finché non completi backfill.

Fase **contract**:
- passa lettura al nuovo schema;
- monitora stabilità;
- rimuovi vecchio schema in release successiva.

**Commento:** evitare migrazioni “breaking in one shot”.

---

### 2) Backfill incrementale e idempotente

Pattern operativo:
- processa record in batch (es. 1k/5k);
- salva checkpoint (`last_processed_id`);
- sleep/throttle tra batch per non saturare DB;
- retry con dead-letter per record invalidi.

```csharp
while (true)
{
    var rows = await repository.GetBatchAsync(lastId, size: 1000, ct);
    if (rows.Count == 0) break;

    foreach (var row in rows)
        await projector.UpsertNewModelAsync(row, ct); // idempotente

    lastId = rows.Max(x => x.Id);
    await checkpointStore.SaveAsync(lastId, ct);
    await Task.Delay(TimeSpan.FromMilliseconds(200), ct); // throttling
}
```

**Commento:** idempotenza è fondamentale per resume sicuro dopo fault.

---

### 3) Compatibilità app multi-versione

Checklist:
- versione N legge/scrive vecchio schema;
- versione N+1 supporta vecchio+nuovo schema;
- deploy rolling senza downtime;
- feature flag per switch lettura nuovo schema.

**Commento:** la compatibilità forward/backward è il cuore del zero-downtime.

---

### 4) Data quality checks

Controlli minimi:
- record count vecchio vs nuovo;
- checksum per campi chiave;
- percentuale null inattesi;
- violazioni vincoli business (es. stato ordine invalido).

**Commento:** non basta “migrazione completata” se i dati non sono affidabili.

---

### 5) Rollback plan (RTO/RPO)

Definisci prima della produzione:
- RTO target (es. < 30 min);
- RPO target (es. 0-5 min);
- trigger di rollback (error rate, mismatch data quality);
- procedure operative e owner.

**Commento:** rollback improvvisato in incidente reale quasi sempre fallisce.

---

### 6) Governance e audit migrazioni

Per ogni migrazione registra:
- scopo;
- rischio;
- finestra esecuzione;
- approvazioni;
- esito e metriche.

**Commento:** audit trail migrazioni aiuta compliance e retrospettive tecniche.

---

## Piano test (obbligatorio)

### Test 1 — Staging realism
1. Esegui migrazione in staging con volume simile a produzione.
2. Verifica tempi e impatto su latenza API.

### Test 2 — Resume after failure
1. Interrompi backfill a metà.
2. Riavvia processo.
3. Verifica ripresa dal checkpoint senza duplicati.

### Test 3 — Dual-write consistency
1. Attiva dual write.
2. Confronta vecchio e nuovo modello su campione significativo.

### Test 4 — Rollback drill
1. Simula trigger rollback.
2. Esegui runbook e misura RTO/RPO reali.

### Test 5 — Contract safety
1. Rimuovi schema legacy solo dopo periodo di stabilità.
2. Verifica nessun client dipendente dal vecchio schema.

---

## Metriche minime da dashboard
- `migration.progress.percent`
- `migration.rows_per_second`
- `migration.error.count`
- `migration.checkpoint.lag`
- `data_quality.mismatch.rate`
- `api.latency.delta_during_migration`

---

## Deliverable richiesti
- Piano expand/contract approvato.
- Tooling backfill idempotente con checkpoint.
- Suite data quality checks automatizzati.
- Runbook rollback con RTO/RPO.
- Report finale consistenza dati.
