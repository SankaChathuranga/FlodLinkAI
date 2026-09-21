using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#pragma warning disable CS8981

namespace FloodLink.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal
                .NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("FloodLink.Domain.Entities.AgentExecutionLog", b =>
            {
                b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
                b.Property<string>("AgentName").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<long>("DurationMs").HasColumnType("bigint");
                b.Property<string>("InputJson").HasColumnType("text");
                b.Property<string>("OutputJson").HasColumnType("text");
                b.Property<string>("Status").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
                b.Property<string>("ToolCallsJson").HasColumnType("text");
                b.Property<Guid>("WorkflowRunId").HasColumnType("uuid");
                b.HasKey("Id");
                b.HasIndex("WorkflowRunId");
                b.ToTable("AgentExecutionLogs");
            });

            modelBuilder.Entity("FloodLink.Domain.Entities.RouteEntity", b =>
            {
                b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
                b.Property<int>("AllocationProposalId").HasColumnType("integer");
                b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<double>("DestLat").HasColumnType("double precision");
                b.Property<double>("DestLng").HasColumnType("double precision");
                b.Property<double>("DistanceKm").HasColumnType("double precision");
                b.Property<double>("EtaMinutes").HasColumnType("double precision");
                b.Property<double>("OriginLat").HasColumnType("double precision");
                b.Property<double>("OriginLng").HasColumnType("double precision");
                b.Property<string>("RoutePolyline").HasColumnType("text");
                b.HasKey("Id");
                b.ToTable("Routes");
            });

            modelBuilder.Entity("FloodLink.Domain.Entities.WorkflowRun", b =>
            {
                b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
                b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("CurrentState").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.Property<string>("Objective").IsRequired().HasMaxLength(1000).HasColumnType("character varying(1000)");
                b.Property<string>("PlanJson").HasColumnType("jsonb");
                b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.ToTable("WorkflowRuns");
            });

            modelBuilder.Entity("FloodLink.Domain.Entities.AgentExecutionLog", b =>
            {
                b.HasOne("FloodLink.Domain.Entities.WorkflowRun", "WorkflowRun")
                    .WithMany()
                    .HasForeignKey("WorkflowRunId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("WorkflowRun");
            });
#pragma warning restore 612, 618
        }
    }
}
