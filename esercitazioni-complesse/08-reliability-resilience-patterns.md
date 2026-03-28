# 08 — Reliability e Resilience Patterns Avanzati

## Scenario
Le dipendenze esterne sono instabili e causano degrado a cascata.

## Obiettivo
Rendere il sistema anti-fragile usando pattern di resilienza evoluti.

## Requisiti vincolanti
- Circuit breaker per dipendenza con soglie dinamiche.
- Timeout budget per catena chiamate distribuite.
- Bulkhead isolation per pool critici.
- Fallback semantici e degraded mode esplicito.
- Hedged requests solo dove il trade-off è favorevole.

## Criteri di accettazione
- Test di resilienza con fault injection automatizzato.
- Riduzione error burst e recovery time misurata.
- Nessun resource exhaustion in scenari di picco + fault.
- Documento decisionale con trade-off e limiti noti.

## Extra hard mode
- Adaptive concurrency limits basati su latenza osservata.
- Simulazione game day multi-team con KPI di esito.
