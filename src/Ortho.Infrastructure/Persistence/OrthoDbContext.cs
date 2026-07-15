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
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<MedicalImage> Images => Set<MedicalImage>();
    public DbSet<ImageAnnotation> Annotations => Set<ImageAnnotation>();
    public DbSet<CephAnalysis> CephAnalyses => Set<CephAnalysis>();
    public DbSet<CephLandmark> CephLandmarks => Set<CephLandmark>();

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

            patient.HasMany(p => p.Images)
                .WithOne()
                .HasForeignKey(i => i.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MedicalImage>(image =>
        {
            image.Property(i => i.FileName).HasMaxLength(260);
            image.Property(i => i.Modality).HasMaxLength(16);
            image.Property(i => i.StorageKeyOriginal).HasMaxLength(512);
            image.Property(i => i.StorageKeyDisplay).HasMaxLength(512);

            image.HasMany(i => i.Annotations)
                .WithOne()
                .HasForeignKey(a => a.MedicalImageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CephAnalysis>(analysis =>
        {
            analysis.Property(a => a.TemplateCode).HasMaxLength(64);
            analysis.Property(a => a.TemplateVersion).HasMaxLength(16);
            analysis.HasIndex(a => new { a.MedicalImageId, a.TemplateCode }).IsUnique();

            analysis.HasOne<MedicalImage>()
                .WithMany()
                .HasForeignKey(a => a.MedicalImageId)
                .OnDelete(DeleteBehavior.Cascade);

            analysis.HasMany(a => a.Landmarks)
                .WithOne()
                .HasForeignKey(l => l.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CephLandmark>(landmark =>
        {
            landmark.Property(l => l.Code).HasMaxLength(16);
            landmark.HasIndex(l => new { l.AnalysisId, l.Code }).IsUnique();
        });

        modelBuilder.Entity<ImageAnnotation>(annotation =>
        {
            annotation.Property(a => a.PointsJson).HasMaxLength(4000);
            annotation.Property(a => a.Text).HasMaxLength(512);
        });

        modelBuilder.Entity<Consultation>().HasIndex(c => c.Date);

        modelBuilder.Entity<PatientDocument>(document =>
        {
            document.Property(d => d.FileName).HasMaxLength(260);
            document.Property(d => d.StorageKey).HasMaxLength(512);
        });

        modelBuilder.Entity<AppUser>(user =>
        {
            user.Property(u => u.Username).HasMaxLength(64);
            user.HasIndex(u => u.Username).IsUnique();
            user.Property(u => u.DisplayName).HasMaxLength(128);
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
