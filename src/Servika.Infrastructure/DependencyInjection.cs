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
using Servika.Application.Abstractions.Kyc;
using Servika.Infrastructure.Kyc;
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

        // Auth/verification policy toggles (e.g. require-phone-before-booking).
        // Off by default; a config flip turns the gate on once the apps prompt.
        services.AddSingleton(
            configuration.GetSection(Servika.Application.Common.AuthPolicyOptions.SectionName)
                .Get<Servika.Application.Common.AuthPolicyOptions>()
            ?? new Servika.Application.Common.AuthPolicyOptions());
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        // Clock is stateless (Singleton); the repository wraps the per-request
        // DbContext, so it must share its Scoped lifetime.
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountEraser, AccountEraser>();

        // Google sign-in: ID-token verification via Google's tokeninfo endpoint.
        // Always registered — with no Google:OAuthClientIds configured it rejects
        // every token, so the endpoint is safely inert until the ids are set.
        var googleOAuthOptions = configuration.GetSection(GoogleOAuthOptions.SectionName).Get<GoogleOAuthOptions>()
            ?? new GoogleOAuthOptions();
        services.AddSingleton(googleOAuthOptions);
        services.AddHttpClient("google-tokeninfo");
        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();

        // Marketplace catalogue (read-only reference data), Scoped (EF).
        services.AddScoped<ICatalogueRepository, CatalogueRepository>();
        services.AddScoped<IArtisanServiceRepository, ArtisanServiceRepository>();

        // Bookings (read + write), Scoped (EF).
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBidRepository, BidRepository>();

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
        services.AddScoped<IVerificationEventRepository, VerificationEventRepository>();
        services.AddScoped<IArtisanGuarantorRepository, ArtisanGuarantorRepository>();
        services.AddScoped<ICustomerRatingRepository, CustomerRatingRepository>();
        // File storage: S3 when a bucket is configured (production — uploads survive
        // redeploys), else the local uploads folder (dev). Same IFileStorage port.
        if (!string.IsNullOrWhiteSpace(configuration["Storage:S3Bucket"]))
            services.AddSingleton<IFileStorage, S3FileStorage>();
        else
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IKycVerificationProvider, ManualKycProvider>();

        // Live-tracking sessions (read + write), Scoped (EF).
        services.AddScoped<ITrackingRepository, TrackingRepository>();

        // Payments + wallet ledger (Scoped, EF).
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();

        // Artisan payouts (Scoped, EF).
        services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();

        // Payment + payout gateways: real Paystack when a key is configured, else
        // stubs (so local dev / tests run the full escrow + payout flow without
        // credentials). Same ports — no use-case changes either way. The one
        // Paystack secret key drives charges, transfers and the bank list.
        var paystackOptions = configuration.GetSection(PaystackOptions.SectionName).Get<PaystackOptions>()
            ?? new PaystackOptions();
        services.AddSingleton(paystackOptions);
        if (paystackOptions.IsConfigured)
        {
            services.AddHttpClient("paystack");
            services.AddSingleton<IPaymentGateway, PaystackPaymentGateway>();
            services.AddSingleton<IPayoutGateway, PaystackPayoutGateway>();
            services.AddSingleton<IBankDirectory, PaystackBankDirectory>();
        }
        else
        {
            services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
            services.AddSingleton<IPayoutGateway, StubPayoutGateway>();
            services.AddSingleton<IBankDirectory, StubBankDirectory>();
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

        // Phone-OTP delivery (WhatsApp-first + SMS fallback via Termii). Real sender
        // when `Sms:ApiKey` is set (env `Sms__ApiKey`), else the dev stub that logs
        // codes — same fallback pattern as Paystack/Resend.
        var smsOptions = configuration.GetSection(SmsOptions.SectionName).Get<SmsOptions>()
            ?? new SmsOptions();
        services.AddSingleton(smsOptions);
        if (smsOptions.IsConfigured)
        {
            services.AddHttpClient("termii");
            services.AddSingleton<IPhoneOtpSender, TermiiPhoneOtpSender>();
        }
        else
        {
            services.AddSingleton<IPhoneOtpSender, StubPhoneOtpSender>();
        }

        // NIN register lookup: Dojah when `Nin:ApiKey` (+ `Nin:AppId`) is set, else the
        // offline stub so the identity screen behaves the same locally.
        var ninOptions = configuration.GetSection(NinOptions.SectionName).Get<NinOptions>() ?? new NinOptions();
        services.AddSingleton(ninOptions);
        if (ninOptions.IsConfigured)
        {
            services.AddHttpClient("dojah");
            services.AddSingleton<INinLookupProvider, DojahNinLookupProvider>();
        }
        else
        {
            services.AddSingleton<INinLookupProvider, StubNinLookupProvider>();
        }

        return services;
    }
}
