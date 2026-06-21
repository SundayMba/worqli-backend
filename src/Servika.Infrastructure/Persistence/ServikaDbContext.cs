using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Servika.Domain.Catalogue;
using Servika.Domain.Users;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// The application's gateway to the database. EF Core turns a DbContext into a
/// live Postgres connection: each <see cref="DbSet{T}"/> below becomes a table,
/// and LINQ queries against them become SQL. This lives in Infrastructure
/// because "how we store things" is an outer-layer detail the Domain never sees.
/// </summary>
public sealed class ServikaDbContext : DbContext
{
    public ServikaDbContext(DbContextOptions<ServikaDbContext> options)
        : base(options)
    {
    }

    /// <summary>The "users" table — one row per account.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>The "refresh_tokens" table — long-lived, revocable credentials.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>The "verification_codes" table — one-time OTPs and reset tokens.</summary>
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

    /// <summary>The "service_categories" table — the curated marketplace catalogue.</summary>
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    /// <summary>The "artisan_profiles" table — public marketplace artisan profiles.</summary>
    public DbSet<ArtisanProfile> ArtisanProfiles => Set<ArtisanProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map the User entity to its table and spell out the column rules.
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");

            user.HasKey(u => u.Id);

            user.Property(u => u.FullName).IsRequired().HasMaxLength(120);
            user.Property(u => u.PhoneNumber).HasMaxLength(20);
            user.Property(u => u.PasswordHash).IsRequired();

            user.Property(u => u.Email).IsRequired().HasMaxLength(256);
            // Enforce one account per email at the database level — the ultimate
            // guard against duplicate registrations, even under a race.
            user.HasIndex(u => u.Email).IsUnique();

            // Store the Role enum as a readable string ("Customer") rather than a
            // bare number, so the database stays legible.
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<RefreshToken>(token =>
        {
            token.ToTable("refresh_tokens");

            token.HasKey(t => t.Id);

            token.Property(t => t.Token).IsRequired().HasMaxLength(200);
            // Look-ups happen by token value, so index it and forbid duplicates.
            token.HasIndex(t => t.Token).IsUnique();

            // Each token belongs to one user; a user can have many tokens. No
            // navigation property is needed on either entity — just the FK. If a
            // user is deleted, their tokens go with them (cascade).
            token.HasOne<User>()
                 .WithMany()
                 .HasForeignKey(t => t.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VerificationCode>(code =>
        {
            code.ToTable("verification_codes");

            code.HasKey(c => c.Id);

            code.Property(c => c.CodeHash).IsRequired().HasMaxLength(128);
            code.Property(c => c.Purpose).HasConversion<string>().HasMaxLength(30);

            // Reset-password looks up by hash; verify/resend look up by user+purpose.
            code.HasIndex(c => c.CodeHash);
            code.HasIndex(c => new { c.UserId, c.Purpose });

            code.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceCategory>(category =>
        {
            category.ToTable("service_categories");

            category.HasKey(c => c.Id);

            category.Property(c => c.Slug).IsRequired().HasMaxLength(60);
            category.HasIndex(c => c.Slug).IsUnique();

            category.Property(c => c.Name).IsRequired().HasMaxLength(80);
            category.Property(c => c.Tint).IsRequired().HasMaxLength(9);
            category.Property(c => c.IconKey).HasMaxLength(60);
        });

        // Structural comparer for the text[] columns: compares by element
        // sequence (not reference), so EF doesn't see the seeded lists as
        // "changed" on every model build (which would force phantom migrations).
        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<ArtisanProfile>(artisan =>
        {
            artisan.ToTable("artisan_profiles");

            artisan.HasKey(a => a.Id);

            artisan.Property(a => a.ImageKey).IsRequired().HasMaxLength(80);
            artisan.Property(a => a.FullName).IsRequired().HasMaxLength(120);
            artisan.Property(a => a.Specialty).IsRequired().HasMaxLength(120);
            artisan.Property(a => a.Accent).IsRequired().HasMaxLength(9);
            artisan.Property(a => a.Location).HasMaxLength(120);
            artisan.Property(a => a.ResponseTime).HasMaxLength(40);
            artisan.Property(a => a.JobsCount).HasMaxLength(20);
            artisan.Property(a => a.About).HasMaxLength(1000);

            // List<string> properties map to native Postgres text[] columns
            // (Npgsql). CategorySlugs is queried with array containment, so index it.
            artisan.Property(a => a.CategorySlugs).Metadata.SetValueComparer(stringListComparer);
            artisan.Property(a => a.Services).Metadata.SetValueComparer(stringListComparer);
            artisan.Property(a => a.GalleryKeys).Metadata.SetValueComparer(stringListComparer);
            artisan.HasIndex(a => a.CategorySlugs).HasMethod("gin");
        });

        // Reference data: seed the catalogue so the marketplace has content.
        CatalogueSeed.Apply(modelBuilder);
    }
}
