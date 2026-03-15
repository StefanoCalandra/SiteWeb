# 02 — CQRS + Outbox + Integrazione Event-Driven

## Scenario
La piattaforma deve pubblicare eventi di dominio affidabili verso servizi esterni senza perdere messaggi o duplicare side effect.

## Obiettivo
Evolvere l'architettura CQRS introducendo pattern Outbox/Inbox con idempotenza e tracciabilità completa.

## Requisiti vincolanti
- Separare command e query model in modo esplicito.
- Aggiungere transazione atomica tra write model e outbox.
- Implementare dispatcher resiliente con retry esponenziale e DLQ.
- Gestire idempotenza dei consumer tramite chiavi deterministiche.
- Versionare i contratti evento (schema evolution compatibile).

## Criteri di accettazione
- Test di failure injection su crash tra commit e publish.
- Nessuna perdita di evento in test di carico con fault.
- Reprocessing sicuro dei messaggi da DLQ.
- Dashboard con lag outbox, tasso retry, error ratio.

## Extra hard mode
- Implementa saga orchestrata e coreografata su due flussi diversi.
- Aggiungi contract-testing asincrono consumer-driven.
