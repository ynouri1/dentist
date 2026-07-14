using Microsoft.EntityFrameworkCore;
using Ortho.Domain.Audit;
using Ortho.Domain.Entities;

namespace Ortho.Infrastructure.Persistence;

public class OrthoDbContext(DbContextOptions<OrthoDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<PatientDocument> Documents => Set<PatientDocument>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>(patient =>
        {
            patient.Property(p => p.FileNumber).HasMaxLength(32);
            patient.HasIndex(p => p.FileNumber).IsUnique();
            patient.Property(p => p.FirstName).HasMaxLength(128);
            patient.Property(p => p.LastName).HasMaxLength(128);
            patient.HasIndex(p => p.LastName);

            patient.HasMany(p => p.Consultations)
                .WithOne()
                .HasForeignKey(c => c.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            patient.HasMany(p => p.Documents)
                .WithOne()
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Consultation>().HasIndex(c => c.Date);

        modelBuilder.Entity<PatientDocument>(document =>
        {
            document.Property(d => d.FileName).HasMaxLength(260);
            document.Property(d => d.StorageKey).HasMaxLength(512);
        });

        modelBuilder.Entity<AuditEntry>(audit =>
        {
            audit.Property(a => a.Action).HasMaxLength(64);
            audit.Property(a => a.EntityType).HasMaxLength(64);
            audit.Property(a => a.EntityId).HasMaxLength(64);
            audit.HasIndex(a => a.TimestampUtc);
        });
    }
}
