# SiteWeb

Progetto dimostrativo che implementa gli esercizi richiesti con una Web API ASP.NET Core e un frontend statico (dashboard real‑time con SignalR, Service Worker e Web Components).

## Cosa contiene

La soluzione è composta da un’unica Web API (`src/SiteWeb.Api`) che integra:
- **Outbox Pattern** con background worker che gestisce retry e dead‑letter.
- **Multi‑tenant** con risoluzione da header (`X-Tenant-Id`) e logging in scope.
- **CQRS + MediatR + FluentValidation** per comandi e query.
- **Auth JWT** con refresh token, revoca e MFA semplificata.
- **API Versioning** (URL e header).
- **GraphQL** con endpoint `/graphql`.
- **SignalR** per dashboard real‑time.
- **Frontend statico** con Web Components e Service Worker (cache).

## Avvio rapido

> Richiede .NET 8 SDK.

```bash
dotnet run --project src/SiteWeb.Api
```

### Endpoint principali
- `GET /health`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/revoke`
- `GET /api/v1/bookings`
- `GET /api/v2/bookings`
- `POST /api/v1/bookings`
- `POST /api/v2/bookings`
- `GET /graphql`
- `GET /` (dashboard statico)

### Header richiesti
- `X-Tenant-Id` per il contesto multi‑tenant.
- `Authorization: Bearer <token>` per endpoint protetti.
- `X-Api-Version` (opzionale) per versioning via header.
