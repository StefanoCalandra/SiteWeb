# 04 — GraphQL Federation, Caching e Cost Control

## Scenario
La sezione GraphQL deve scalare con richieste complesse e prevenire query abusive.

## Obiettivo
Portare il layer GraphQL a livello enterprise con federazione, performance tuning e protezioni anti-abuso.

## Requisiti vincolanti
- Implementare persisted queries obbligatorie in produzione.
- Aggiungere query cost analysis con limiti dinamici per tenant.
- Ridurre N+1 con DataLoader e caching selettivo.
- Implementare invalidazione cache event-driven.
- Versionare schema federato con deprecazioni controllate.

## Criteri di accettazione
- Profiling con baseline e miglioramento misurabile (>40% latenza p95).
- Blocchi automatici su query ad alto costo.
- Test su coerenza cache in aggiornamenti concorrenti.
- Report di compatibilità schema per client legacy.

## Extra hard mode
- Introduci edge caching con chiavi composte da tenant + scope.
- Canary release di modifiche schema con osservabilità dedicata.
