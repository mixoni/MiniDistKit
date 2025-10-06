using Microsoft.EntityFrameworkCore;

namespace MiniDist.Api;

public class SagaOrchestrator : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SagaOrchestrator> _log;

    public SagaOrchestrator(IServiceProvider sp, ILogger<SagaOrchestrator> log)
    {
        _sp = sp; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("SagaOrchestrator started.");
        using var scope = _sp.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await foreach (var msg in bus.SubscribeAllAsync(stoppingToken))
        {
            await using var s = _sp.CreateAsyncScope();
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var mbus = s.ServiceProvider.GetRequiredService<IMessageBus>();

            switch (msg)
            {
                case ClaimCreated e:
                    await StartSagaAsync(db, e.ClaimId, stoppingToken);
                    break;

                case PaymentReceived e:
                    await ActivateClaimAsync(db, mbus, e.ClaimId, stoppingToken);
                    break;

                case PaymentFailed e:
                    await CompensateAsync(db, mbus, e.ClaimId, e.Reason, stoppingToken);
                    break;
            }
        }
    }

    private static async Task StartSagaAsync(AppDbContext db, int claimId, CancellationToken ct)
    {
        var corr = claimId.ToString();
        var state = await db.Sagas.FirstOrDefaultAsync(x => x.CorrelationId == corr, ct);
        if (state is null)
        {
            db.Sagas.Add(new SagaState { CorrelationId = corr, State = "AwaitingPayment" });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task ActivateClaimAsync(AppDbContext db, IMessageBus bus, int claimId, CancellationToken ct)
    {
        var claim = await db.Claims.FirstOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return;

        claim.Status = "Active";
        db.Sagas.Single(x => x.CorrelationId == claimId.ToString()).State = "Activated";

        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new ClaimActivated(claimId, DateTime.UtcNow), ct);
    }

    private static async Task CompensateAsync(AppDbContext db, IMessageBus bus, int claimId, string reason, CancellationToken ct)
    {
        var claim = await db.Claims.FirstOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return;

        claim.Status = "Reverted";
        db.Sagas.Single(x => x.CorrelationId == claimId.ToString()).State = "Compensated";

        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new ClaimReverted(claimId, reason, DateTime.UtcNow), ct);
    }
}
