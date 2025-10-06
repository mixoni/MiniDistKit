using Microsoft.EntityFrameworkCore;

namespace MiniDist.Api;

public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxDispatcher> _log;

    public OutboxDispatcher(IServiceProvider sp, ILogger<OutboxDispatcher> log)
    {
        _sp = sp;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("OutboxDispatcher started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

                var batch = await db.Outbox
                    .Where(x => x.DispatchedUtc == null)
                    .OrderBy(x => x.CreatedUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var msg in batch)
                {
                    // demo: znamo jedan tip (ClaimCreated). U praksi mapiraj po msg.Type.
                    if (msg.Type.EndsWith(nameof(ClaimCreated)))
                    {
                        var evt = msg.Deserialize<ClaimCreated>();
                        await bus.PublishAsync(evt, stoppingToken);
                        _log.LogInformation("Outbox -> Published ClaimCreated for ClaimId={Id}", evt.ClaimId);
                    }

                    msg.DispatchedUtc = DateTime.UtcNow;
                }

                if (batch.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
                else
                    await Task.Delay(300, stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "OutboxDispatcher error");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
