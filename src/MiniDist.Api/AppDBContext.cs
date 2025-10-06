using Microsoft.EntityFrameworkCore;

namespace MiniDist.Api;

public class AppDbContext : DbContext
{
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> Processed => Set<ProcessedMessage>();
    public DbSet<SagaState> Sagas => Set<SagaState>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Claim>().HasKey(x => x.Id);

        b.Entity<OutboxMessage>().HasKey(x => x.Id);
        b.Entity<OutboxMessage>().Property(x => x.Type).IsRequired();
        b.Entity<OutboxMessage>().Property(x => x.PayloadJson).IsRequired();
        b.Entity<OutboxMessage>().HasIndex(x => x.DispatchedUtc);

        b.Entity<ProcessedMessage>().HasKey(x => x.Id);
        b.Entity<ProcessedMessage>().HasIndex(x => x.MessageId).IsUnique();

        b.Entity<SagaState>().HasKey(x => x.Id);
        b.Entity<SagaState>().HasIndex(x => x.CorrelationId).IsUnique();
    }
}
