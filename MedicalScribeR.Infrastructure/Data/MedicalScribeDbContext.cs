using Microsoft.EntityFrameworkCore;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Infrastructure.Data
{
    /// <summary>
    /// Contexto do banco de dados para o MedicalScribeR
    /// </summary>
    public class MedicalScribeDbContext : DbContext
    {
        public MedicalScribeDbContext(DbContextOptions<MedicalScribeDbContext> options) : base(options)
        {
        }

        // Sessões de transcrição
        public DbSet<TranscriptionSession> TranscriptionSessions { get; set; }
        
        // Chunks de transcrição
        public DbSet<TranscriptionChunk> TranscriptionChunks { get; set; }
        
        // Documentos gerados
        public DbSet<GeneratedDocument> GeneratedDocuments { get; set; }
        
        // Itens de ação
        public DbSet<ActionItem> ActionItems { get; set; }
        
        // Logs de processamento
        public DbSet<ProcessingLog> ProcessingLogs { get; set; }
        
        // Entidades médicas extraídas
        public DbSet<HealthcareEntity> HealthcareEntities { get; set; }

        // Configurações de agentes
        public DbSet<AgentConfiguration> AgentConfigurations { get; set; }

        // Logs de auditoria
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração da TranscriptionSession
            modelBuilder.Entity<TranscriptionSession>(entity =>
            {
                entity.HasKey(e => e.SessionId);
                entity.Property(e => e.SessionId).HasMaxLength(100);
                entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PatientName).HasMaxLength(200);
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.StartedAt).IsRequired();
                entity.Property(e => e.CompletedAt);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.StartedAt);
                entity.HasIndex(e => e.Status);
            });

            // Configuração da TranscriptionChunk
            modelBuilder.Entity<TranscriptionChunk>(entity =>
            {
                entity.HasKey(e => e.ChunkId);
                entity.Property(e => e.ChunkId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Text).IsRequired();
                entity.Property(e => e.Speaker).HasMaxLength(100);
                entity.Property(e => e.Confidence).HasColumnType("decimal(5,4)").HasConversion(v => (double)v, v => (decimal)v);
                entity.Property(e => e.Timestamp).IsRequired();

                entity.HasOne<TranscriptionSession>()
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.Timestamp);
            });

            // Configuração da GeneratedDocument
            modelBuilder.Entity<GeneratedDocument>(entity =>
            {
                entity.HasKey(e => e.DocumentId);
                entity.Property(e => e.DocumentId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.GeneratedBy).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.IsApproved).HasDefaultValue(false);

                entity.HasOne<TranscriptionSession>()
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.CreatedAt);
            });

            // Configuração da ActionItem
            modelBuilder.Entity<ActionItem>(entity =>
            {
                entity.HasKey(e => e.ActionId);
                entity.Property(e => e.ActionId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.Priority).HasMaxLength(20).IsRequired();
                entity.Property(e => e.DueDate);
                entity.Property(e => e.IsCompleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).IsRequired();

                entity.HasOne<TranscriptionSession>()
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => e.IsCompleted);
            });

            // Configuração da ProcessingLog
            modelBuilder.Entity<ProcessingLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.LogId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.AgentName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Details);
                entity.Property(e => e.ErrorMessage);
                entity.Property(e => e.IsSuccess).IsRequired();
                entity.Property(e => e.Duration).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();
                entity.Property(e => e.InputData);
                entity.Property(e => e.OutputData);

                entity.HasOne<TranscriptionSession>()
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.AgentName);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.IsSuccess);
            });

            // Configuração da HealthcareEntity
            modelBuilder.Entity<HealthcareEntity>(entity =>
            {
                entity.HasKey(e => e.EntityId);
                entity.Property(e => e.EntityId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Text).IsRequired();
                entity.Property(e => e.Category).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SubCategory).HasMaxLength(100);
                entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(5,4)").HasConversion(v => (double)v, v => (decimal)v);
                entity.Property(e => e.Offset);
                entity.Property(e => e.Length);

                entity.HasOne<TranscriptionSession>()
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.Category);
            });

            // Configuração da AgentConfiguration
            modelBuilder.Entity<AgentConfiguration>(entity =>
            {
                entity.HasKey(e => e.AgentName);
                entity.Property(e => e.AgentName).HasMaxLength(100);
                entity.Property(e => e.IsEnabled).IsRequired();
                entity.Property(e => e.ConfidenceThreshold).HasColumnType("float");
                entity.Property(e => e.TriggeringIntentions).IsRequired();
                entity.Property(e => e.Prompt);
                entity.Property(e => e.LastUpdated).IsRequired();
            });

            // Configuração da AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.LogId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
                entity.Property(e => e.EntityType).HasMaxLength(100);
                entity.Property(e => e.EntityId).HasMaxLength(100);
                entity.Property(e => e.Details);
                entity.Property(e => e.Timestamp).IsRequired();
                entity.Property(e => e.IpAddress).HasMaxLength(45);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.Timestamp);
            });

            // Dados iniciais para AgentConfigurations
            modelBuilder.Entity<AgentConfiguration>().HasData(
                new AgentConfiguration
                {
                    AgentName = "SummaryAgent",
                    IsEnabled = true,
                    ConfidenceThreshold = 0.8,
                    TriggeringIntentions = "Summarize,Conclusion,Review",
                    Prompt = "Gere um resumo conciso da consulta médica em português brasileiro:",
                    LastUpdated = DateTime.UtcNow
                },
                new AgentConfiguration
                {
                    AgentName = "PrescriptionAgent",
                    IsEnabled = true,
                    ConfidenceThreshold = 0.8,
                    TriggeringIntentions = "Prescription,Medication,Treatment",
                    Prompt = "Extraia e estruture as prescrições médicas mencionadas:",
                    LastUpdated = DateTime.UtcNow
                },
                new AgentConfiguration
                {
                    AgentName = "DiagnosisAgent",
                    IsEnabled = true,
                    ConfidenceThreshold = 0.8,
                    TriggeringIntentions = "Diagnosis,Condition,Assessment",
                    Prompt = "Identifique e organize os diagnósticos ou suspeitas diagnósticas:",
                    LastUpdated = DateTime.UtcNow
                },
                new AgentConfiguration
                {
                    AgentName = "FollowUpAgent",
                    IsEnabled = true,
                    ConfidenceThreshold = 0.8,
                    TriggeringIntentions = "FollowUp,NextSteps,Return",
                    Prompt = "Identifique ações de follow-up e próximos passos:",
                    LastUpdated = DateTime.UtcNow
                }
            );
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Configuração de fallback para desenvolvimento
                optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MedicalScribeR;Trusted_Connection=true;");
            }

            // Configurações de performance
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableServiceProviderCaching();
            optionsBuilder.EnableDetailedErrors(false);
        }

        public override int SaveChanges()
        {
            AddTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AddTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void AddTimestamps()
        {
            var entities = ChangeTracker.Entries()
                .Where(x => x.Entity is ITimestamped && (x.State == EntityState.Added || x.State == EntityState.Modified));

            foreach (var entity in entities)
            {
                var timestamped = (ITimestamped)entity.Entity;

                if (entity.State == EntityState.Added)
                {
                    timestamped.CreatedAt = DateTime.UtcNow;
                }

                if (entity.State == EntityState.Modified)
                {
                    timestamped.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }

    /// <summary>
    /// Interface para entidades que possuem timestamps
    /// </summary>
    public interface ITimestamped
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
}
