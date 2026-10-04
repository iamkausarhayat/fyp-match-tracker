using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Models;

namespace FypMatchTracker.Data;

/// <summary>
/// Entity Framework Core Database Context.
/// Maps the 7 tables defined in the ER diagram to the SQLite relational database.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // 1. User accounts table (Student & Coordinator roles)
    public DbSet<AppUser> Users { get; set; }

    // 2. Department table (CS, SE, AI, IT)
    public DbSet<Department> Departments { get; set; }

    // 3. Past and reserved FYP projects repository
    public DbSet<FypProject> FypProjects { get; set; }

    // 4. Research papers fetched from Crossref Web API
    public DbSet<ExternalPaper> ExternalPapers { get; set; }

    // 5. Submitted student similarity checks
    public DbSet<SimilarityCheck> SimilarityChecks { get; set; }

    // 6. Closest matches for each check
    public DbSet<SimilarityMatch> SimilarityMatches { get; set; }

    // 7. Thresholds and AI formula weights configuration
    public DbSet<SimilaritySetting> SimilaritySettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique constraint on User Email
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Unique constraint on Research Paper DOI
        modelBuilder.Entity<ExternalPaper>()
            .HasIndex(p => p.Doi)
            .IsUnique();

        // Relationship: Department -> FypProjects (One to Many)
        modelBuilder.Entity<FypProject>()
            .HasOne(f => f.Department)
            .WithMany(d => d.FypProjects)
            .HasForeignKey(f => f.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship: Student Owner -> FypProjects (One to Many, Optional)
        modelBuilder.Entity<FypProject>()
            .HasOne(f => f.Owner)
            .WithMany(u => u.FypProjects)
            .HasForeignKey(f => f.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship: Student -> SimilarityChecks (One to Many, Optional for guest checks)
        modelBuilder.Entity<SimilarityCheck>()
            .HasOne(c => c.Student)
            .WithMany(u => u.SimilarityChecks)
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship: SimilarityCheck -> SimilarityMatches (One to Many, Cascade delete)
        modelBuilder.Entity<SimilarityMatch>()
            .HasOne(m => m.SimilarityCheck)
            .WithMany(c => c.Matches)
            .HasForeignKey(m => m.CheckId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship: SimilarityMatch -> FypProject (Optional link)
        modelBuilder.Entity<SimilarityMatch>()
            .HasOne(m => m.FypProject)
            .WithMany(f => f.SimilarityMatches)
            .HasForeignKey(m => m.FypProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship: SimilarityMatch -> ExternalPaper (Optional link)
        modelBuilder.Entity<SimilarityMatch>()
            .HasOne(m => m.ExternalPaper)
            .WithMany(p => p.SimilarityMatches)
            .HasForeignKey(m => m.ExternalPaperId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
