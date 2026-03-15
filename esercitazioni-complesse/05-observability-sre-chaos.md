# 05 — Observability, SRE e Chaos Engineering

## Scenario
Il sistema deve offrire affidabilità elevata e diagnosi rapida durante incidenti.

## Obiettivo
Costruire osservabilità completa (logs, metrics, traces) e validare resilienza con chaos testing.

## Requisiti vincolanti
- OpenTelemetry end-to-end con propagazione trace context.
- SLI/SLO formalizzati per latenza, errori, disponibilità.
- Alerting actionabile con runbook e ownership.
- Chaos experiment periodici su DB, broker e dipendenze esterne.
- Error budget policy con gate su release.

## Criteri di accettazione
- Incident drill con MTTR ridotto rispetto baseline.
- Dashboards orientate a golden signals.
- Postmortem blameless con azioni preventive tracciate.
- Copertura tracing >90% delle rotte business critiche.

## Extra hard mode
- A/B test di strategie retry/circuit breaker con analisi comparativa.
- Introduci auto-remediation controllata su alert frequenti.
