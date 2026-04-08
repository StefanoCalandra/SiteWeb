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

### 1) Pianificazione migrazione con fasi esplicite

Fasi standard:
1. **Design**: schema target + analisi impatto client.
2. **Expand**: aggiunta strutture compatibili.
3. **Backfill**: popolamento graduale nuovo modello.
4. **Switch**: lettura dal nuovo modello via feature flag.
5. **Contract**: rimozione schema legacy.

Checklist pre-go-live:
- volume stimato da migrare;
- finestra operativa;
- owner tecnici e business;
- criteri stop/rollback;
- approvazioni change.

**Commento:** il fallimento delle migrazioni è spesso organizzativo, non solo tecnico.

---

### 2) Strategia expand/contract (safe schema evolution)

Fase **expand**:
- aggiungi nuove colonne/tabelle senza rompere la versione attuale;
- non rimuovere colonne legacy nella stessa release;
- mantieni default/nullable compatibili.

Fase **contract**:
- solo dopo periodo di stabilità (es. 2 release);
- verifica assenza dipendenze residue;
- rimuovi vecchio schema con rollout controllato.

**Commento:** “one-shot migration” è il pattern più rischioso su sistemi in produzione.

---

### 3) Dual write controllata con feature flag

```csharp
public sealed class OrderWriter
{
    private readonly IFeatureFlagService _flags;
    private readonly LegacyOrderRepository _legacy;
    private readonly NewOrderRepository _modern;

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        await _legacy.SaveAsync(order, ct); // percorso legacy sempre attivo in fase iniziale

        if (await _flags.IsEnabledAsync("orders.dual_write", ct))
        {
            await _modern.UpsertAsync(order, ct); // shadow write controllata
        }
    }
}
```

**Commento:** dual write va monitorata con mismatch counter, non solo “best effort”.

---

### 4) Backfill incrementale, idempotente e resumable

Pattern operativo:
- batch size dinamico (es. 500-5000);
- checkpoint persistente (`last_processed_id`);
- throttling per proteggere DB;
- retry con DLQ record problematici;
- metriche avanzamento in tempo reale.

```csharp
while (true)
{
    var rows = await repository.GetBatchAsync(lastId, size: 1000, ct);
    if (rows.Count == 0) break;

    foreach (var row in rows)
        await projector.UpsertNewModelAsync(row, ct); // idempotente

    lastId = rows.Max(x => x.Id);
    await checkpointStore.SaveAsync(lastId, ct);

    await Task.Delay(TimeSpan.FromMilliseconds(200), ct); // throttle
}
```

**Commento:** idempotenza e checkpoint sono obbligatori per ripartenza sicura dopo crash.

---

### 5) Compatibilità multi-versione applicativa

Regole:
- versione N continua a funzionare su schema legacy;
- versione N+1 legge da legacy/new in base a flag;
- deploy rolling senza downtime;
- switch progressivo per tenant/canary.

Esempio read switch:

```csharp
public async Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct)
{
    if (await _flags.IsEnabledAsync("orders.read_new_model", ct))
        return await _modern.ReadAsync(id, ct);

    return await _legacy.ReadAsync(id, ct);
}
```

**Commento:** lo switch per tenant riduce blast radius durante transizione.

---

### 6) Data quality checks automatici

Controlli minimi:
- record count vecchio vs nuovo;
- checksum/hash su campi business chiave;
- nullability violations inattese;
- vincoli business (totali, stati, timeline).

Esempio output check:
- `count_mismatch = 0`
- `checksum_mismatch_rate < 0.1%`
- `business_rule_violations = 0`

**Commento:** “migrazione finita” senza quality gate non significa migrazione corretta.

---

### 7) Rollback plan con RTO/RPO misurabili

Definisci prima del go-live:
- RTO target (es. < 30 minuti);
- RPO target (es. 0-5 minuti);
- trigger rollback (error rate, mismatch rate, latenza eccessiva);
- comandi operativi già testati;
- owner per ogni step.

Rollback rapido tipico:
1. disattiva `read_new_model`;
2. disattiva `dual_write`;
3. verifica stabilizzazione KPI;
4. apri incident + postmortem.

**Commento:** rollback efficace richiede esercitazione periodica (drill), non solo documento.

---

### 8) Governance, audit e compliance

Per ogni migrazione registra:
- scopo e rischio;
- approvazioni e change ticket;
- finestra esecuzione;
- esito con metriche;
- deviazioni da piano;
- azioni correttive post-run.

**Commento:** audit trail completo semplifica compliance e apprendimento organizzativo.

---

## Piano test (obbligatorio)

### Test 1 — Staging realism
1. Esegui migrazione in staging con volume simile a produzione.
2. Verifica tempi, impatto latenza e carico DB.

### Test 2 — Resume after failure
1. Interrompi backfill a metà.
2. Riavvia processo.
3. Verifica ripresa da checkpoint senza duplicati.

### Test 3 — Dual-write consistency
1. Attiva dual write.
2. Confronta legacy/new model su campione significativo.
3. Verifica mismatch rate entro soglia.

### Test 4 — Read switch canary
1. Attiva `read_new_model` su 5% tenant.
2. Verifica KPI invariati.
3. Estendi progressivamente al 100%.

### Test 5 — Rollback drill
1. Simula trigger rollback.
2. Esegui runbook.
3. Misura RTO/RPO reali rispetto target.

### Test 6 — Contract safety
1. Verifica assenza dipendenze al legacy schema.
2. Rimuovi campi legacy in release separata.

---

## Metriche minime da dashboard
- `migration.progress.percent`
- `migration.rows_per_second`
- `migration.error.count`
- `migration.checkpoint.lag`
- `dual_write.mismatch.rate`
- `read_new_model.error.rate`
- `data_quality.mismatch.rate`
- `api.latency.delta_during_migration`

---

## Deliverable richiesti
- Piano migrazione (design -> contract) approvato.
- Tooling backfill idempotente con checkpoint.
- Feature flag plan (dual write/read switch) per rollout canary.
- Suite automatica data quality checks.
- Runbook rollback con RTO/RPO + evidenza drill.
- Report finale consistenza dati e lessons learned.
