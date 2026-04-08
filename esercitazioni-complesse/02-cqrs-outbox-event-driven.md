# 02 — CQRS + Outbox + Integrazione Event-Driven

## Scenario
La piattaforma deve pubblicare eventi di dominio affidabili verso servizi esterni senza perdere messaggi o duplicare side effect.

## Obiettivo
Evolvere l'architettura CQRS introducendo pattern Outbox/Inbox con idempotenza e tracciabilità completa.

---

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

---

## Svolgimento guidato (con commenti)

> Nota: questa è una **soluzione di riferimento commentata** da adattare alla codebase reale.

### 1) CQRS esplicito: command model vs query model

```csharp
// Command side: regole di dominio + scrittura stato
public sealed record CreateOrderCommand(string CustomerId, IReadOnlyList<CreateOrderLine> Lines);

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ISystemClock _clock;

    public CreateOrderHandler(AppDbContext db, ISystemClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId, cmd.Lines, _clock.UtcNow);

        _db.Orders.Add(order);

        // Evento di dominio che poi diventerà messaggio Outbox
        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, order.CustomerId, order.Total));

        await _db.SaveChangesAsync(ct);
        return order.Id;
    }
}
```

```csharp
// Query side: modello ottimizzato lettura (anche denormalizzato)
public sealed record OrderSummaryQuery(Guid OrderId);

public sealed class OrderSummaryQueryHandler : IQueryHandler<OrderSummaryQuery, OrderSummaryDto?>
{
    private readonly ReadDbContext _readDb;

    public OrderSummaryQueryHandler(ReadDbContext readDb) => _readDb = readDb;

    public Task<OrderSummaryDto?> Handle(OrderSummaryQuery query, CancellationToken ct)
        => _readDb.OrderSummaries
            .Where(x => x.OrderId == query.OrderId)
            .Select(x => new OrderSummaryDto(x.OrderId, x.CustomerName, x.Total, x.Status))
            .SingleOrDefaultAsync(ct);
}
```

**Commento:** la separazione è utile solo se reale: niente shortcut che usano il write model per query critiche.

---

### 2) Outbox atomico nella stessa transazione

```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;          // es. "order.created.v1"
    public string PayloadJson { get; init; } = default!;   // JSON serializzato evento
    public DateTime OccurredUtc { get; init; }
    public DateTime? ProcessedUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
```

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    // 1) Raccogli domain events dalle aggregate modificate
    var domainEvents = ChangeTracker.Entries<AggregateRoot>()
        .SelectMany(e => e.Entity.DequeueDomainEvents())
        .ToList();

    // 2) Mappa domain event -> OutboxMessage prima del commit
    foreach (var evt in domainEvents)
    {
        OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = EventTypeMapper.Map(evt),
            PayloadJson = JsonSerializer.Serialize(evt),
            OccurredUtc = DateTime.UtcNow
        });
    }

    // 3) Un solo commit: stato dominio + outbox atomici
    return await base.SaveChangesAsync(ct);
}
```

**Commento:** se il DB commit riesce, anche l'outbox è persistita. Eviti il buco classico “dato scritto ma evento perso”.

---

### 3) Dispatcher outbox resiliente (retry + DLQ)

```csharp
public sealed class OutboxDispatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            var batch = await db.OutboxMessages
                .Where(x => x.ProcessedUtc == null && x.AttemptCount < 10)
                .OrderBy(x => x.OccurredUtc)
                .Take(100)
                .ToListAsync(stoppingToken);

            foreach (var msg in batch)
            {
                try
                {
                    await bus.PublishAsync(msg.Type, msg.PayloadJson, stoppingToken);
                    msg.ProcessedUtc = DateTime.UtcNow; // successo definitivo
                }
                catch (Exception ex)
                {
                    msg.AttemptCount++;
                    msg.LastError = ex.Message;

                    // Dopo N tentativi sposta in DLQ logica (tabella o broker dedicato)
                    if (msg.AttemptCount >= 10)
                    {
                        db.DeadLetterMessages.Add(new DeadLetterMessage
                        {
                            Id = Guid.NewGuid(),
                            SourceOutboxId = msg.Id,
                            Type = msg.Type,
                            PayloadJson = msg.PayloadJson,
                            Reason = ex.Message,
                            FailedUtc = DateTime.UtcNow
                        });

                        msg.ProcessedUtc = DateTime.UtcNow; // marcato "chiuso" nel main outbox
                    }
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
```

**Commento:** retry va con backoff/jitter; qui semplificato. Importante evitare hot-loop quando il broker è down.

---

### 4) Inbox consumer + idempotenza forte

```csharp
public sealed class InboxMessage
{
    public Guid Id { get; init; }
    public string MessageKey { get; init; } = default!; // chiave idempotenza (es. EventId o aggregateId+version)
    public string Type { get; init; } = default!;
    public DateTime ProcessedUtc { get; init; }
}
```

```csharp
public async Task HandleAsync(BrokerMessage message, CancellationToken ct)
{
    // message.Key dev'essere deterministica e stabile
    var alreadyProcessed = await _db.InboxMessages.AnyAsync(x => x.MessageKey == message.Key, ct);
    if (alreadyProcessed)
        return; // Idempotenza: duplicate delivery ignorata in sicurezza

    await using var tx = await _db.Database.BeginTransactionAsync(ct);

    // 1) Applica side effect locale
    await _projectionUpdater.ApplyAsync(message, ct);

    // 2) Registra inbox nello stesso commit
    _db.InboxMessages.Add(new InboxMessage
    {
        Id = Guid.NewGuid(),
        MessageKey = message.Key,
        Type = message.Type,
        ProcessedUtc = DateTime.UtcNow
    });

    await _db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
}
```

**Commento:** senza inbox atomica con i side-effect, i duplicati prima o poi rompono consistenza.

---

### 5) Versioning dei contratti evento

Convenzione consigliata:
- `order.created.v1`
- `order.created.v2`

Regole operative:
- non rompere campi esistenti (aggiungi solo opzionali quando possibile);
- mantieni consumer backward-compatible per una finestra di transizione;
- inserisci `schemaVersion` nel payload e validazione all’ingresso.

**Commento:** evoluzione schema va trattata come API pubblica, con deprecazione pianificata.

---

## Piano test (obbligatorio)

### Test 1 — Crash tra commit e publish
1. Forza eccezione subito dopo `SaveChanges` command side.
2. Riavvia dispatcher.
3. Verifica che evento presente in outbox venga pubblicato al restart.

### Test 2 — At-least-once + idempotenza
1. Simula duplicate delivery (stesso `message.Key` inviato due volte).
2. Verifica un solo side-effect persistito.

### Test 3 — DLQ e reprocessing
1. Simula consumer esterno sempre fallente.
2. Verifica incremento `AttemptCount` e spostamento in DLQ.
3. Correggi causa, rilancia reprocessing DLQ, verifica successo.

### Test 4 — Load con fault
1. Esegui carico con fault random broker/db.
2. Verifica nessuna perdita evento e latenza outbox entro soglia.

---

## Metriche minime da dashboard
- `outbox.pending.count` (lag corrente)
- `outbox.publish.success.rate`
- `outbox.publish.retry.count`
- `outbox.dlq.count`
- `inbox.dedup.hit.rate`
- `event.end_to_end.latency.ms` (occurred -> consumed)

**Commento:** senza metriche end-to-end non sai se stai “pubblicando” o “consegnando valore”.

---

## Deliverable richiesti
- Implementazione command/query split con cartelle e naming chiari.
- Outbox + dispatcher + DLQ + inbox idempotente.
- Test di fault injection e carico con report risultati.
- ADR con scelte su semantica delivery (`at-least-once`) e trade-off.
