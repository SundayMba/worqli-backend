using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;
using Servika.Domain.Payments;
using Servika.Domain.Tracking;
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

    /// <summary>The "bookings" table — one row per customer service request.</summary>
    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>The "payments" table — one row per payment attempt against a booking.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <summary>The "wallet_transactions" table — the append-only money ledger.</summary>
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    /// <summary>The "tracking_sessions" table — one row per artisan trip (live tracking).</summary>
    public DbSet<TrackingSession> TrackingSessions => Set<TrackingSession>();

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

            // Optional link to the artisan's login account. Looked up when matching
            // a signed-in artisan to their jobs, so index it. If the user is ever
            // deleted the profile survives (the link just goes null).
            artisan.HasIndex(a => a.UserId);
            artisan.HasOne<User>()
                   .WithMany()
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.ToTable("bookings");

            booking.HasKey(b => b.Id);

            // A customer's history is listed by customer id + creation time.
            booking.HasIndex(b => b.CustomerId);

            booking.Property(b => b.CategorySlug).IsRequired().HasMaxLength(60);
            booking.Property(b => b.ServiceName).IsRequired().HasMaxLength(80);
            booking.Property(b => b.ArtisanName).HasMaxLength(120);
            booking.Property(b => b.Description).IsRequired().HasMaxLength(2000);
            booking.Property(b => b.AddressText).IsRequired().HasMaxLength(300);
            booking.Property(b => b.LocationInstructions).HasMaxLength(300);
            booking.Property(b => b.PreferredTimeSlot).HasMaxLength(60);

            // Money: commission rate is a fraction (0–1); store with enough scale.
            booking.Property(b => b.CommissionRate).HasPrecision(5, 4);

            // Enums kept as readable strings, matching users/catalogue.
            booking.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
            booking.Property(b => b.Urgency).HasConversion<string>().HasMaxLength(20);
            booking.Property(b => b.PricingModel).HasConversion<string>().HasMaxLength(20);
            booking.Property(b => b.PaymentState).HasConversion<string>().HasMaxLength(20);

            // The customer is a User; bookings die with the user (cascade). The
            // optional artisan points at catalogue reference data (not yet a User
            // account), so it is a plain nullable column with no FK for now.
            booking.HasOne<User>()
                   .WithMany()
                   .HasForeignKey(b => b.CustomerId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("payments");

            payment.HasKey(p => p.Id);

            // The webhook finds the payment by our reference — unique + indexed.
            payment.Property(p => p.Reference).IsRequired().HasMaxLength(80);
            payment.HasIndex(p => p.Reference).IsUnique();
            payment.HasIndex(p => p.BookingId);

            payment.Property(p => p.Provider).IsRequired().HasMaxLength(30);
            payment.Property(p => p.AuthorizationUrl).HasMaxLength(500);
            payment.Property(p => p.CommissionRate).HasPrecision(5, 4);
            payment.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

            payment.HasOne<Booking>()
                   .WithMany()
                   .HasForeignKey(p => p.BookingId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WalletTransaction>(txn =>
        {
            txn.ToTable("wallet_transactions");

            txn.HasKey(t => t.Id);

            // Balances are summed per (owner type, owner id) — index that.
            txn.HasIndex(t => new { t.OwnerType, t.OwnerId });

            txn.Property(t => t.OwnerType).HasConversion<string>().HasMaxLength(20);
            txn.Property(t => t.Type).HasConversion<string>().HasMaxLength(30);
            txn.Property(t => t.Description).IsRequired().HasMaxLength(200);
            // No FK to bookings/payments: the ledger is an immutable record that must
            // survive even if a related row is ever removed.
        });

        modelBuilder.Entity<TrackingSession>(session =>
        {
            session.ToTable("tracking_sessions");

            session.HasKey(s => s.Id);

            session.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

            // The hub looks up the active session for a booking. A filtered unique
            // index both serves that query and enforces one active session per
            // booking (an ended session never blocks a new trip).
            session.HasIndex(s => s.BookingId)
                   .IsUnique()
                   .HasFilter("\"Status\" = 'Active'")
                   .HasDatabaseName("UX_tracking_active_per_booking");

            // The session belongs to a booking; it dies with the booking (cascade).
            session.HasOne<Booking>()
                   .WithMany()
                   .HasForeignKey(s => s.BookingId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // Reference data: seed the catalogue so the marketplace has content.
        CatalogueSeed.Apply(modelBuilder);
    }
}
