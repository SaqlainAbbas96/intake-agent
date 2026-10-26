using System.Text.RegularExpressions;

using Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class IntakeDbContext(DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ExtractionRequest> ExtractionRequests => Set<ExtractionRequest>();
    public DbSet<ExtractionResult> ExtractionResults => Set<ExtractionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ExtractionRequest>(e =>
        {
            e.ToTable("extraction_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.SchemaName).HasMaxLength(100).IsRequired();
            e.Property(x => x.RawInput).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        modelBuilder.Entity<ExtractionResult>(e =>
        {
            e.ToTable("extraction_results");
            e.HasKey(x => x.Id);
            e.Property(x => x.ExtractedJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Confidence).HasPrecision(5, 4);
            e.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            e.Property(x => x.Model).HasMaxLength(100).IsRequired();
            e.Property(x => x.CostUsd).HasPrecision(12, 6);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ExtractionRequest>().WithOne().HasForeignKey<ExtractionResult>(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RequestId).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        // snake_case column names, so the SQL stays readable
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
