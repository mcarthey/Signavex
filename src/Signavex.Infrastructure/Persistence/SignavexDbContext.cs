using Microsoft.EntityFrameworkCore;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Persistence;

public class SignavexDbContext : DbContext
{
    public SignavexDbContext(DbContextOptions<SignavexDbContext> options) : base(options) { }

    public DbSet<ScanRunEntity> ScanRuns => Set<ScanRunEntity>();
    public DbSet<ScanCandidateEntity> ScanCandidates => Set<ScanCandidateEntity>();
    public DbSet<ScanCheckpointEntity> ScanCheckpoints => Set<ScanCheckpointEntity>();
    public DbSet<ScanCommandEntity> ScanCommands => Set<ScanCommandEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScanRunEntity>(e =>
        {
            e.ToTable("ScanRuns");
            e.HasKey(x => x.Id);
            e.Property(x => x.ScanId).IsRequired();
            e.HasIndex(x => x.ScanId).IsUnique();
            e.HasIndex(x => x.CompletedAtUtc);
            e.HasMany(x => x.Candidates)
                .WithOne(x => x.ScanRun)
                .HasForeignKey(x => x.ScanRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScanCandidateEntity>(e =>
        {
            e.ToTable("ScanCandidates");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Ticker);
            e.HasIndex(x => new { x.ScanRunId, x.Ticker }).IsUnique();
        });

        modelBuilder.Entity<ScanCheckpointEntity>(e =>
        {
            e.ToTable("ScanCheckpoints");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ScanCommandEntity>(e =>
        {
            e.ToTable("ScanCommands");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestedAtUtc);
        });
    }
}
