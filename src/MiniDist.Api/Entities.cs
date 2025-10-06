using System.Text.Json;

namespace MiniDist.Api;

public class Claim
{
    public int Id { get; set; }
    public string PolicyNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Created";
    public DateTime CreatedUtc { get; set; }
}

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedUtc { get; set; }

    public static OutboxMessage From<T>(T @event)
        => new() { Type = typeof(T).FullName!, PayloadJson = JsonSerializer.Serialize(@event) };

    public T Deserialize<T>() => JsonSerializer.Deserialize<T>(PayloadJson)!;
}

public class ProcessedMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MessageId { get; set; } = default!; // idempotency key (event id or business id)
    public DateTime ProcessedUtc { get; set; } = DateTime.UtcNow;
}

public class SagaState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CorrelationId { get; set; } = default!; // e.g. ClaimId as string
    public string State { get; set; } = "New";
    public string? DataJson { get; set; }
}
