using CodeScanner.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeScanner.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<ScanFile> ScanFiles => Set<ScanFile>();
    public DbSet<Finding> Findings => Set<Finding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Scan>(e =>
        {
            e.HasMany(s => s.Files)
                .WithOne(f => f.Scan)
                .HasForeignKey(f => f.ScanId)
                .OnDelete(DeleteBehavior.Cascade);

            e.Property(s => s.Status).HasConversion<string>();
        });

        modelBuilder.Entity<ScanFile>(e =>
        {
            e.HasMany(f => f.Findings)
                .WithOne(f => f.ScanFile)
                .HasForeignKey(f => f.ScanFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Finding>(e =>
        {
            e.Property(f => f.Category).HasConversion<string>();
            e.Property(f => f.Severity).HasConversion<string>();
        });
    }
}
