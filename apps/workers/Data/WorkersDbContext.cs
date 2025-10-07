using Microsoft.EntityFrameworkCore;
using Hostr.Workers.Models;
using Pgvector.EntityFrameworkCore;

namespace Hostr.Workers.Data;

public class WorkersDbContext : DbContext
{
    public WorkersDbContext(DbContextOptions<WorkersDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<FAQ> FAQs { get; set; }
    public DbSet<KnowledgeBaseChunk> KnowledgeBaseChunks { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<UsageDaily> UsageDaily { get; set; }
    public DbSet<Conversation> Conversations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KnowledgeBaseChunk>(entity =>
        {
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
        });

        modelBuilder.Entity<UsageDaily>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Date }).IsUnique();
        });
    }
}