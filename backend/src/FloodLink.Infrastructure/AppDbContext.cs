using FloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Infrastructure;

/// <summary>
/// The single EF Core database context for FloodLink AI.
/// Registers entities owned by Member C (WorkflowRuns, AgentExecutionLog, Routes).
/// Other members will add their own entity registrations here during schema finalization
/// (Week 2 team task).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── Member C tables ────────────────────────────────────────────────────────

    /// <summary>All workflow pipeline runs.</summary>
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();

    /// <summary>Per-agent execution log entries for observability and audit.</summary>
    public DbSet<AgentExecutionLog> AgentExecutionLogs => Set<AgentExecutionLog>();

    /// <summary>Routing results calculated by the Route/ETA Agent.</summary>
    public DbSet<RouteEntity> Routes => Set<RouteEntity>();

    // ── Placeholder DbSets for other members' tables ──────────────────────────
    // TODO (Member A — Week 2): Register Shelters, Reports, TriagePlans DbSets here
    //   after confirming schema against Section 3.1.
    // TODO (Member B — Week 2): Register Depots, InventoryItems, AllocationProposals DbSets here.
    // TODO (Member D — Week 2): Register Dispatches, ValidationResults, AuditTrail DbSets here.

    public DbSet<Depot> Depots => Set<Depot>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<AllocationProposal> AllocationProposals => Set<AllocationProposal>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── WorkflowRun ────────────────────────────────────────────────────────
        modelBuilder.Entity<WorkflowRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Objective).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.PlanJson).HasColumnType("jsonb");
            entity.Property(e => e.CurrentState)
                  .HasConversion<string>()
                  .HasMaxLength(50);
            entity.Property(e => e.FailedAtState)
                  .HasConversion<string?>()
                  .HasMaxLength(50)
                  .IsRequired(false);
        });

        // ── AgentExecutionLog ──────────────────────────────────────────────────
        modelBuilder.Entity<AgentExecutionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            // TODO: Switch InputJson/OutputJson/ToolCallsJson to HasColumnType("jsonb")
            //       once the team confirms jsonb vs text for Postgres.
            entity.HasOne(e => e.WorkflowRun)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowRunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RouteEntity ────────────────────────────────────────────────────────
        modelBuilder.Entity<RouteEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.DistanceMeters).IsRequired();
            entity.Property(e => e.EstimatedDurationSeconds).IsRequired();
            entity.Property(e => e.PolylineString).IsRequired().HasColumnType("text");
            entity.HasOne(e => e.WorkflowRun)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowRunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Depot, InventoryItem, AllocationProposal ───────────────────────────
        modelBuilder.Entity<Depot>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(d => d.Latitude)
                .HasPrecision(10, 7);

            entity.Property(d => d.Longitude)
                .HasPrecision(10, 7);

            entity.HasMany(d => d.InventoryItems)
                .WithOne(i => i.Depot)
                .HasForeignKey(i => i.DepotId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.AllocationProposals)
                .WithOne(a => a.Depot)
                .HasForeignKey(a => a.DepotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.ItemName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(i => i.Unit)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(i => i.QuantityAvailable)
                .HasPrecision(18, 2);

            entity.Property(i => i.QuantityReserved)
                .HasPrecision(18, 2);

            entity.Property(i => i.ReorderThreshold)
                .HasPrecision(18, 2);
        });

        modelBuilder.Entity<AllocationProposal>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.ItemName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(a => a.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(a => a.Quantity)
                .HasPrecision(18, 2);
        });
    }
}

