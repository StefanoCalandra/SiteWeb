using Microsoft.EntityFrameworkCore;
using SiteWeb.Api.Data;
using SiteWeb.Api.Entities;

namespace SiteWeb.Api.Messaging;

public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceProvider serviceProvider, ILogger<OutboxDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Ciclo principale: prova a pubblicare messaggi pendenti.
            await DispatchPendingMessages(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task DispatchPendingMessages(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var messages = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.OccurredAt)
            .Take(20)
            .ToListAsync(stoppingToken);

        foreach (var message in messages)
        {
            try
            {
                // Simulazione pubblicazione su broker (RabbitMQ/Service Bus).
                _logger.LogInformation("Publishing outbox message {MessageId} of type {Type}", message.Id, message.Type);
                message.Status = OutboxStatus.Sent;
                message.NextAttemptAt = null;
            }
            catch (Exception ex)
            {
                // Backoff esponenziale per retry.
                message.RetryCount += 1;
                message.NextAttemptAt = now.AddSeconds(Math.Pow(2, message.RetryCount));
                _logger.LogWarning(ex, "Failed to publish outbox message {MessageId}", message.Id);

                if (message.RetryCount >= 5)
                {
                    // Dead-letter: segnala fallimento definitivo.
                    message.Status = OutboxStatus.Failed;
                }
            }
        }

        await db.SaveChangesAsync(stoppingToken);
    }
}
