using Microsoft.EntityFrameworkCore;

namespace PackageManager.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageVersion> PackageVersions => Set<PackageVersion>();
    public DbSet<IgnoredFile> IgnoredFiles => Set<IgnoredFile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Package>(e =>
        {
            e.HasIndex(p => p.PackageId).IsUnique();
            e.Property(p => p.PackageId).UseCollation("NOCASE");
            e.HasMany(p => p.Versions).WithOne(v => v.Package).HasForeignKey(v => v.PackageDbId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PackageVersion>(e => e.HasIndex(v => v.RelativePath).IsUnique());
        b.Entity<IgnoredFile>(e => e.HasIndex(f => f.RelativePath).IsUnique());
    }
}
