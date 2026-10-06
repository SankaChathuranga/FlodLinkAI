using System;
using System.Collections.Generic;
using FloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Infrastructure;

/// <summary>
/// The single EF Core database context for FloodLink AI.
/// Registers entities owned by Member C (WorkflowRuns, AgentExecutionLog, Routes)
/// and Member A (Users, Shelters, Reports, TriagePlans).
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

    // ── Member A tables ────────────────────────────────────────────────────────

    /// <summary>Minimal Users table for FK relationships.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Emergency shelter locations.</summary>
    public DbSet<Shelter> Shelters => Set<Shelter>();

    /// <summary>Field reports submitted by volunteers.</summary>
    public DbSet<Report> Reports => Set<Report>();

    /// <summary>Generated triage plans.</summary>
    public DbSet<TriagePlanEntity> TriagePlans => Set<TriagePlanEntity>();

    // ── Placeholder DbSets for other members' tables ──────────────────────────
    // TODO (Member B — Week 2): Register Depots, InventoryItems, AllocationProposals DbSets here.
    // TODO (Member D — Week 2): Register Dispatches, ValidationResults, AuditTrail DbSets here.

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

        // ── User ───────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Phone).IsRequired().HasMaxLength(20);
            entity.Property(e => e.HashedPassword).IsRequired().HasMaxLength(255);
        });

        // ── Shelter ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Shelter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(e => e.ContactVolunteer)
                  .WithMany(u => u.ContactShelters)
                  .HasForeignKey(e => e.ContactVolunteerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Report ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NeedType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PhotoUrl).HasMaxLength(500);

            entity.HasOne(e => e.Shelter)
                  .WithMany(s => s.Reports)
                  .HasForeignKey(e => e.ShelterId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Reporter)
                  .WithMany(u => u.SubmittedReports)
                  .HasForeignKey(e => e.ReportedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── TriagePlanEntity ───────────────────────────────────────────────────
        modelBuilder.Entity<TriagePlanEntity>(entity =>
        {
            entity.ToTable("TriagePlans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GeneratedFromReportIds).HasColumnType("jsonb");
            entity.Property(e => e.PlanSummaryJson).HasColumnType("jsonb");

            entity.HasOne(e => e.CreatedByAgentRun)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedByAgentRunId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Seed Data ──────────────────────────────────────────────────────────
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var fixedDate = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Name = "John Doe",
                Role = "Volunteer",
                Phone = "+94771234567",
                HashedPassword = "hashed_password_1",
                CreatedAt = fixedDate
            },
            new User
            {
                Id = 2,
                Name = "Jane Smith",
                Role = "Coordinator",
                Phone = "+94779876543",
                HashedPassword = "hashed_password_2",
                CreatedAt = fixedDate
            }
        );

        modelBuilder.Entity<Shelter>().HasData(
            new Shelter
            {
                Id = 1,
                Name = "Central Colombo Relief Center",
                Latitude = 6.9271,
                Longitude = 79.8612,
                Capacity = 200,
                CurrentOccupancy = 140,
                ContactVolunteerId = 1,
                Status = "Active",
                CreatedAt = fixedDate,
                UpdatedAt = fixedDate
            },
            new Shelter
            {
                Id = 2,
                Name = "Kaduwela Community Hall",
                Latitude = 6.9344,
                Longitude = 79.9841,
                Capacity = 150,
                CurrentOccupancy = 120,
                ContactVolunteerId = 1,
                Status = "Active",
                CreatedAt = fixedDate,
                UpdatedAt = fixedDate
            },
            new Shelter
            {
                Id = 3,
                Name = "Gampaha Primary School Shelter",
                Latitude = 7.0840,
                Longitude = 79.9925,
                Capacity = 100,
                CurrentOccupancy = 45,
                ContactVolunteerId = 2,
                Status = "Active",
                CreatedAt = fixedDate,
                UpdatedAt = fixedDate
            }
        );

        modelBuilder.Entity<Report>().HasData(
            new Report
            {
                Id = 1,
                ShelterId = 1,
                ReportedBy = 1,
                NeedType = "Water",
                QuantityNeeded = 500,
                UrgencyLevel = 5,
                PhotoUrl = "https://storage.floodlink.lk/reports/photo_1.jpg",
                GpsLat = 6.9271,
                GpsLng = 79.8612,
                Status = "New",
                CreatedAt = fixedDate
            },
            new Report
            {
                Id = 2,
                ShelterId = 1,
                ReportedBy = 1,
                NeedType = "Food",
                QuantityNeeded = 300,
                UrgencyLevel = 4,
                PhotoUrl = "https://storage.floodlink.lk/reports/photo_2.jpg",
                GpsLat = 6.9271,
                GpsLng = 79.8612,
                Status = "Triaged",
                CreatedAt = fixedDate
            },
            new Report
            {
                Id = 3,
                ShelterId = 2,
                ReportedBy = 2,
                NeedType = "Medical",
                QuantityNeeded = 50,
                UrgencyLevel = 5,
                PhotoUrl = null,
                GpsLat = 6.9344,
                GpsLng = 79.9841,
                Status = "New",
                CreatedAt = fixedDate
            },
            new Report
            {
                Id = 4,
                ShelterId = 2,
                ReportedBy = 1,
                NeedType = "Shelter-Repair",
                QuantityNeeded = 20,
                UrgencyLevel = 3,
                PhotoUrl = "https://storage.floodlink.lk/reports/photo_4.jpg",
                GpsLat = 6.9344,
                GpsLng = 79.9841,
                Status = "InPlan",
                CreatedAt = fixedDate
            },
            new Report
            {
                Id = 5,
                ShelterId = 3,
                ReportedBy = 2,
                NeedType = "Other",
                QuantityNeeded = 100,
                UrgencyLevel = 2,
                PhotoUrl = null,
                GpsLat = 7.0840,
                GpsLng = 79.9925,
                Status = "Resolved",
                CreatedAt = fixedDate
            }
        );
    }
}
