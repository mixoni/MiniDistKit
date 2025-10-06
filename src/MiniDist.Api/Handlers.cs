using Microsoft.EntityFrameworkCore;

namespace MiniDist.Api;

public class PaymentsHandler : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<PaymentsHandler> _log;

    public PaymentsHandler(IServiceProvider sp, ILogger<PaymentsHandler> log)
    {
        _sp = sp;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("PaymentsHandler started.");
        using var scope = _sp.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await foreach (var msg in bus.SubscribeAllAsync(stoppingToken))
        {
            if (msg is not ClaimCreated created) continue;

            await using var s = _sp.CreateAsyncScope();
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var saga = s.ServiceProvider.GetRequiredService<IMessageBus>(); // reuse bus for next step

            var idempotencyKey = $"payment:{created.ClaimId}";
            if (await db.Processed.AnyAsync(x => x.MessageId == idempotencyKey, stoppingToken))
            {
                _log.LogInformation("PaymentHandler dedup ClaimId={Id}", created.ClaimId);
                continue; 
            }

            // Demo "payments": succeeds if Amount < 1000, otherwise fails
            var ok = created.Amount < 1000m;

            if (ok)
            {
                await saga.PublishAsync(new PaymentReceived(created.ClaimId, created.Amount, DateTime.UtcNow), stoppingToken);
                _log.LogInformation("Payment OK for ClaimId={Id}", created.ClaimId);
            }
            else
            {
                await saga.PublishAsync(new PaymentFailed(created.ClaimId, "Amount too high", DateTime.UtcNow), stoppingToken);
                _log.LogWarning("Payment FAILED for ClaimId={Id}", created.ClaimId);
            }

            db.Processed.Add(new ProcessedMessage { MessageId = idempotencyKey });
            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
