using deviceAgent.model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.data
{
    internal class AgentDbContext:DbContext
    {
        public DbSet<CardInventory>CardInventories => Set<CardInventory>();
        public DbSet<DeviceStatus> DeviceStatuses => Set<DeviceStatus>();
        public DbSet<EventQueue> EventQueues => Set<EventQueue>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<CardJob> CardJobs => Set<CardJob>();
       

        public AgentDbContext(DbContextOptions<AgentDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. CARD INVENTORY
            modelBuilder.Entity<CardInventory>(entity =>
            {
                entity.ToTable("card_inventory");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FeederCassetteNo).IsUnique();

                entity.Property(e => e.CardType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LowThreshold).HasDefaultValue(5);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 2. TRANSACTIONS
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("transactions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TransactionId).IsUnique();

                entity.Property(e => e.TransactionId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 3. CARD JOBS
            modelBuilder.Entity<CardJob>(entity =>
            {
                entity.ToTable("card_jobs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TransactionId);

                entity.Property(e => e.EncodingStatus).HasDefaultValue("PENDING").HasMaxLength(20);
                entity.Property(e => e.PrintingStatus).HasDefaultValue("PENDING").HasMaxLength(20);
                entity.Property(e => e.DispenseStatus).HasDefaultValue("PENDING").HasMaxLength(20);
                entity.Property(e => e.IsRejected).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relation avec Transaction
                entity.HasOne(d => d.Transaction)
                      .WithMany(p => p.CardJobs)
                      .HasForeignKey(d => d.TransactionId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relation avec CardInventory (Clé étrangère sur FeederCassetteNo)
                entity.HasOne(d => d.FeederInventory)
                      .WithMany()
                      .HasPrincipalKey(p => p.FeederCassetteNo)
                      .HasForeignKey(d => d.FeederUsed)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 4. DEVICE STATUS
            modelBuilder.Entity<DeviceStatus>(entity =>
            {
                entity.ToTable("device_status");
                entity.HasKey(e => e.ComponentName);

                entity.Property(e => e.ComponentName).HasMaxLength(50);
                entity.Property(e => e.StatusCode).HasMaxLength(30);
                entity.Property(e => e.HealthState).HasMaxLength(20);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 5. EVENT QUEUE (Outbox Pattern)
            modelBuilder.Entity<EventQueue>(entity =>
            {
                entity.ToTable("event_queue");
                entity.HasKey(e => e.Id);

                // Index partiel/filtré pour dépiler ultra-rapidement les évènements non traités
                entity.HasIndex(e => new { e.IsProcessed, e.CreatedAt })
                      .HasFilter("is_processed = 0");

                entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PayloadJson).IsRequired();
                entity.Property(e => e.IsProcessed).HasDefaultValue(false);
                entity.Property(e => e.RetryCount).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 6. AUDIT LOGS
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("audit_logs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.Component).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StatusCode).IsRequired().HasMaxLength(30);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

        }

        /// <summary>
        /// À exécuter au démarrage de l'Agent pour appliquer les optimisations SQLite.
        /// </summary>
        public static async Task ApplyKioskSqlitePragmasAsync(AgentDbContext context, CancellationToken ct = default)
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", ct);
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", ct);
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", ct);
            await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout = 5000;", ct);
        }

    }
}
