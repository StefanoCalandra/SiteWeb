namespace SiteWeb.Api.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int RetryCount { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextAttemptAt { get; set; }
}

public enum OutboxStatus
{
    Pending,
    Sent,
    Failed
}
