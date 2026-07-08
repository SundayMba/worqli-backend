using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servika.Application.Abstractions.Directions;
using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Abstractions.Verification;
using Servika.Infrastructure.Directions;
using Servika.Infrastructure.Notifications;
using Servika.Infrastructure.Payments;
using Servika.Infrastructure.Persistence;
using Servika.Infrastructure.Security;
using Servika.Infrastructure.Storage;
using Servika.Infrastructure.Time;
using Servika.Infrastructure.Verification;

namespace Servika.Infrastructure;

/// <summary>
/// One place where the Infrastructure layer registers everything it provides
/// (database, and later: hashing, JWT, payments) into the app's DI container.
/// The API calls <c>AddInfrastructure(...)</c> and stays unaware of EF Core.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        services.AddDbContext<ServikaDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Password hashing. Stateless and thread-safe, so one shared instance
        // for the whole app (Singleton) is correct and cheapest.
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // Read the "Jwt" config section once into a strongly-typed object, and
        // share it + the token generator as Singletons (both are stateless).
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        // Clock is stateless (Singleton); the repository wraps the per-request
        // DbContext, so it must share its Scoped lifetime.
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Marketplace catalogue (read-only reference data), Scoped (EF).
        services.AddScoped<ICatalogueRepository, CatalogueRepository>();

        // Bookings (read + write), Scoped (EF).
        services.AddScoped<IBookingRepository, BookingRepository>();

        // Reviews (customer ratings on completed bookings), Scoped (EF).
        services.AddScoped<IReviewRepository, ReviewRepository>();

        // In-app notifications feed, Scoped (EF).
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Push notifications: device tokens (Scoped EF) + Expo sender + fire-and-forget
        // dispatcher (Singletons — they use IHttpClientFactory / IServiceScopeFactory).
        services.AddScoped<IPushTokenRepository, PushTokenRepository>();
        services.AddHttpClient("expo-push");
        services.AddSingleton<IPushSender, ExpoPushSender>();
        services.AddSingleton<INotificationPushDispatcher, NotificationPushDispatcher>();

        // Referrals (attribution + reward pool), Scoped (EF).
        services.AddScoped<IReferralRepository, ReferralRepository>();

        // Disputes (customer complaints + admin resolution), Scoped (EF).
        services.AddScoped<IDisputeRepository, DisputeRepository>();

        // Favourites (customer's saved artisans), Scoped (EF).
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();

        // Chat (per-booking customer↔artisan conversations), Scoped (EF).
        services.AddScoped<IChatRepository, ChatRepository>();

        // Platform settings (single admin-controlled row), Scoped (EF).
        services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();

        // Artisan KYC (submission store + file storage + verification provider).
        services.AddScoped<IArtisanKycRepository, ArtisanKycRepository>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IKycVerificationProvider, ManualKycProvider>();

        // Live-tracking sessions (read + write), Scoped (EF).
        services.AddScoped<ITrackingRepository, TrackingRepository>();

        // Payments + wallet ledger (Scoped, EF).
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();

        // Artisan payouts (Scoped, EF). Disbursement uses the stub gateway until
        // Paystack Transfers is wired (same IPayoutGateway port either way).
        services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
        services.AddSingleton<IPayoutGateway, StubPayoutGateway>();

        // Payment gateway: real Paystack when a key is configured, else the stub
        // (so local dev / tests run the full escrow flow without credentials).
        // Same port (IPaymentGateway) — no use-case changes either way.
        var paystackOptions = configuration.GetSection(PaystackOptions.SectionName).Get<PaystackOptions>()
            ?? new PaystackOptions();
        services.AddSingleton(paystackOptions);
        if (paystackOptions.IsConfigured)
        {
            services.AddHttpClient("paystack");
            services.AddSingleton<IPaymentGateway, PaystackPaymentGateway>();
        }
        else
        {
            services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
        }

        // Directions: real Google Directions when a key is configured, else a
        // straight-line stub (so the tracking map works in local dev without
        // credentials). Same IDirectionsProvider port — no use-case changes. The
        // key is server-side only; the mobile app calls our /tracking/route.
        var googleOptions = configuration.GetSection(GoogleDirectionsOptions.SectionName).Get<GoogleDirectionsOptions>()
            ?? new GoogleDirectionsOptions();
        services.AddSingleton(googleOptions);
        if (googleOptions.IsConfigured)
        {
            services.AddHttpClient("google-directions");
            services.AddSingleton<IDirectionsProvider, GoogleDirectionsProvider>();
        }
        else
        {
            services.AddSingleton<IDirectionsProvider, StubDirectionsProvider>();
        }

        // OTP / password-reset: code generation+hashing (stateless → Singleton)
        // and the code repository (Scoped, EF).
        services.AddSingleton<IOtpService, OtpService>();
        services.AddScoped<IVerificationCodeRepository, VerificationCodeRepository>();

        // OTP delivery: real email via Resend when an API key is configured,
        // otherwise the dev logger (so local dev needs no secret). Both are
        // Singletons implementing the same IOtpSender port.
        var resendOptions = configuration.GetSection(ResendOptions.SectionName).Get<ResendOptions>()
            ?? new ResendOptions();
        services.AddSingleton(resendOptions);
        if (resendOptions.IsConfigured)
        {
            services.AddHttpClient("resend");
            services.AddSingleton<IOtpSender, ResendEmailSender>();
        }
        else
        {
            services.AddSingleton<IOtpSender, LoggingOtpSender>();
        }

        return services;
    }
}
