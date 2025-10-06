using Microsoft.EntityFrameworkCore;
using MiniDist.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("mini"));
builder.Services.AddSingleton<IMessageBus, InMemoryMessageBus>();

builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<SagaOrchestrator>();
builder.Services.AddHostedService<PaymentsHandler>();

// Idempotency filter (optional for API commands) – uses Idempotency-Key header
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddScoped<IdempotencyMiddleware>();

var app = builder.Build();

app.UseMiddleware<IdempotencyMiddleware>();

app.MapGet("/debug/claims", async (AppDbContext db) =>
    Results.Ok(await db.Claims.AsNoTracking().OrderBy(c => c.Id).ToListAsync()));

app.MapGet("/debug/outbox", async (AppDbContext db) =>
    Results.Ok(await db.Outbox.AsNoTracking().OrderBy(o => o.CreatedUtc).ToListAsync()));

app.MapGet("/debug/processed", async (AppDbContext db) =>
    Results.Ok(await db.Processed.AsNoTracking().OrderBy(p => p.ProcessedUtc).ToListAsync()));

app.MapGet("/debug/sagas", async (AppDbContext db) =>
    Results.Ok(await db.Sagas.AsNoTracking().OrderBy(s => s.Id).ToListAsync()));

// REPLAY: re-publish ClaimCreated for an existing claim (test consumer idempotency)
app.MapPost("/debug/replay/{id:int}", async (int id, AppDbContext db, IMessageBus bus) =>
{
    var claim = await db.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    if (claim is null) return Results.NotFound();

    var evt = new ClaimCreated(claim.Id, claim.PolicyNumber, claim.Amount, DateTime.UtcNow);
    await bus.PublishAsync(evt, default);
    return Results.Ok(new { replayed = evt });
});

app.MapPost("/claims", async (CreateClaim cmd, AppDbContext db, CancellationToken ct) =>
{
    // 1) Local transaction: Claim + Outbox
    var claim = new Claim { PolicyNumber = cmd.PolicyNumber, Amount = cmd.Amount, Status = "Created", CreatedUtc = DateTime.UtcNow };
    db.Claims.Add(claim);

    var evt = new ClaimCreated(claim.Id, claim.PolicyNumber, claim.Amount, DateTime.UtcNow);
    db.Outbox.Add(OutboxMessage.From(evt));

    await db.SaveChangesAsync(ct);

    return Results.Accepted($"/claims/{claim.Id}", new { claim.Id, claim.Status });
});

app.MapGet("/", () => "MiniDistKit running. POST /claims");

app.Run();

public record CreateClaim(string PolicyNumber, decimal Amount);
