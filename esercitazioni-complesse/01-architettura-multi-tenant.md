# 01 — Architettura Multi-Tenant Enterprise

## Scenario
`SiteWeb.Api` deve servire più tenant enterprise con isolamento logico forte, configurazioni diverse e policy di sicurezza dedicate.

## Obiettivo
Implementare una piattaforma multi-tenant robusta con supporto a:
- risoluzione tenant per host/header/claim;
- isolamento dati per tenant;
- feature flag a livello tenant;
- policy autorizzative dinamiche per tenant.

## Requisiti vincolanti
- Implementare `TenantResolutionPipeline` con fallback ordinato.
- Evitare leakage dati con query filter globali e test di isolamento.
- Gestire caching per tenant con namespace separato.
- Fornire bootstrap automatico tenant (seed dati + ruoli base).
- Aggiungere audit trail tenant-aware.

## Criteri di accettazione
- Suite di test di integrazione che valida isolamento end-to-end.
- Nessuna query cross-tenant in test di sicurezza.
- Feature toggle attivabile/disattivabile runtime senza deploy.
- Documentazione tecnica con diagrammi C4 livello container/component.

## Extra hard mode
- Introduci sharding per tenant premium.
- Aggiungi piano di migrazione da single-tenant a multi-tenant senza downtime.
