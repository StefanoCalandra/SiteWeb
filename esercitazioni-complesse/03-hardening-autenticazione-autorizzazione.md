# 03 — Hardening di Autenticazione e Autorizzazione

## Scenario
L'API deve rispettare standard elevati per clienti enterprise e audit compliance.

## Obiettivo
Rafforzare sicurezza applicativa su identità, autorizzazione e difesa in profondità.

## Requisiti vincolanti
- Implementare rotating signing keys (JWKS) e gestione key rollover.
- Policy-based authorization con requisiti contestuali (tenant, ruolo, rischio).
- Rate limit e adaptive throttling per endpoint sensibili.
- Rilevamento token replay e revoca near-real-time.
- Hardening header HTTP + CORS least-privilege.

## Criteri di accettazione
- Threat model STRIDE aggiornato e mitigazioni tracciate.
- Pentest checklist automatizzata in pipeline.
- Test negativi su privilege escalation e broken access control.
- Logging sicurezza con correlazione utente/sessione/request.

## Extra hard mode
- Introduci step-up authentication per operazioni ad alto rischio.
- Aggiungi motore ABAC con policy dichiarative.
