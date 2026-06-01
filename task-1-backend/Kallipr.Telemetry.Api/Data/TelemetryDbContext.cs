using Kallipr.Telemetry.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kallipr.Telemetry.Api.Data;

public class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : base(options) { }

    public DbSet<TelemetryReading> Readings => Set<TelemetryReading>(); //creating our table of readings

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryReading>(entity =>
        {
            entity.HasKey(r => r.Id);                                           //PK -> Unique identifier for each reading
            entity.Property(r => r.TenantId).IsRequired().HasMaxLength(100);
            entity.Property(r => r.DeviceId).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Type).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Unit).IsRequired().HasMaxLength(50);
            entity.Property(r => r.ExternalId).IsRequired().HasMaxLength(100);


            entity.HasIndex(r => new { r.TenantId, r.ExternalId })    //prevent duplicates when extID is repeated for the same tenant
                  .IsUnique()                                        // no two rows can have the same TenantId + ExternalId combination
                  .HasDatabaseName("IX_Readings_TenantId_ExternalId");

             entity.HasIndex(r => new { r.TenantId, r.DeviceId, r.RecordedAt })  // Bookmark columns so searches are faster, not a full table scan
                  .HasDatabaseName("IX_Readings_TenantId_DeviceId_RecordedAt");
        });
    }
}
