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

    // ── Member D tables ────────────────────────────────────────────────────────

    /// <summary>Coordinator decisions that commit (or reject) a validated plan.</summary>
    public DbSet<Dispatch> Dispatches => Set<Dispatch>();

    /// <summary>Persisted per-check outputs of the Validation/Safety Agent.</summary>
    public DbSet<ValidationResult> ValidationResults => Set<ValidationResult>();

    /// <summary>Append-only audit log for the approval/dispatch lifecycle.</summary>
    public DbSet<AuditTrail> AuditTrail => Set<AuditTrail>();

    // ── Placeholder DbSets for other members' tables ──────────────────────────
    // TODO (Member A — Week 2): Register Shelters, Reports, TriagePlans DbSets here
    //   after confirming schema against Section 3.1.
    // TODO (Member B — Week 2): Register Depots, InventoryItems, AllocationProposals DbSets here.

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
            // TODO: AllocationProposalId FK relationship to be wired once Member B
            //       registers AllocationProposals — add HasOne/WithMany here.
        });

        // ── Dispatch ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Dispatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Decision)
                  .HasConversion<string>()
                  .HasMaxLength(30);
            entity.Property(e => e.ApprovalNotes).HasMaxLength(2000);
            entity.HasIndex(e => e.WorkflowRunId).IsUnique();
            entity.HasOne(e => e.WorkflowRun)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowRunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ValidationResult ───────────────────────────────────────────────────
        modelBuilder.Entity<ValidationResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CheckName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ViolationDetail).HasMaxLength(2000);
            entity.HasIndex(e => e.WorkflowRunId);
            entity.HasOne(e => e.WorkflowRun)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowRunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── AuditTrail ─────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditTrail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EventDetailJson).HasColumnType("jsonb");
            entity.HasIndex(e => e.DispatchId);
            entity.HasOne(e => e.Dispatch)
                  .WithMany()
                  .HasForeignKey(e => e.DispatchId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
