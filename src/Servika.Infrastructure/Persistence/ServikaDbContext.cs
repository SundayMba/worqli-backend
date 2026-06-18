using Microsoft.EntityFrameworkCore;
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
    }
}
