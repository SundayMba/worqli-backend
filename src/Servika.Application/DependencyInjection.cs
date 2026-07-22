using Microsoft.Extensions.DependencyInjection;
using Servika.Application.Bookings;
using Servika.Application.Catalogue;
using Servika.Application.Payments;
using Servika.Application.Reviews;
using Servika.Application.Users.BecomeArtisan;
using Servika.Application.Users.Login;
using Servika.Application.Users.Logout;
using Servika.Application.Users.Me;
using Servika.Application.Users.Otp;
using Servika.Application.Users.Password;
using Servika.Application.Users.Refresh;
using Servika.Application.Users.Register;

namespace Servika.Application;

/// <summary>
/// Registers the Application layer's use-case handlers into the DI container.
/// They're Scoped because they depend on the per-request repository/DbContext.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<Users.Delete.DeleteAccountHandler>();
        services.AddScoped<VerifyOtpHandler>();
        services.AddScoped<ResendOtpHandler>();
        services.AddScoped<SendPhoneOtpHandler>();
        services.AddScoped<VerifyPhoneOtpHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<BecomeArtisanHandler>();
        services.AddScoped<Users.Profile.UpdateProfileHandler>();
        services.AddScoped<Users.Google.GoogleLoginHandler>();

        // Catalogue (marketplace) query handlers.
        services.AddScoped<GetCategoriesHandler>();
        services.AddScoped<GetArtisansHandler>();
        services.AddScoped<GetArtisanByIdHandler>();
        services.AddScoped<GetArtisanPhotoHandler>();
        services.AddScoped<AddGalleryPhotoHandler>();
        services.AddScoped<RemoveGalleryPhotoHandler>();
        services.AddScoped<GetArtisanGalleryPhotoHandler>();

        // Artisan self-onboarding (create/read their own marketplace profile).
        services.AddScoped<GetMyArtisanProfileHandler>();
        services.AddScoped<GetMyArtisanServicesHandler>();
        services.AddScoped<SaveArtisanServiceHandler>();
        services.AddScoped<DeleteArtisanServiceHandler>();
        services.AddScoped<SetArtisanAvailabilityHandler>();
        services.AddScoped<SaveArtisanProfileHandler>();

        // Artisan KYC (submit documents, read status).
        services.AddScoped<SubmitKycHandler>();
        services.AddScoped<GetKycStatusHandler>();

        // Booking use-case handlers.
        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<GetMyBookingsHandler>();
        services.AddScoped<GetBookingByIdHandler>();
        services.AddScoped<SubmitBidHandler>();
        services.AddScoped<GetMyBidHandler>();
        services.AddScoped<GetBookingBidsHandler>();
        services.AddScoped<AcceptBidHandler>();
        services.AddScoped<GetBookingMediaHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<ChoosePaymentMethodHandler>();
        services.AddScoped<RebroadcastBookingHandler>();
        services.AddScoped<CompleteBookingHandler>();
        services.AddScoped<SubmitJobCompletionHandler>();
        services.AddScoped<GetJobCompletionHandler>();
        services.AddScoped<AutoConfirmCompletionsHandler>();

        // Artisan-side booking handlers (jobs assigned to the signed-in artisan).
        services.AddScoped<GetArtisanJobsHandler>();
        services.AddScoped<GetArtisanJobByIdHandler>();
        services.AddScoped<AdvanceBookingByArtisanHandler>();

        // Open (unassigned) requests: the pool a matching artisan can browse + claim.
        services.AddScoped<GetOpenJobsHandler>();
        services.AddScoped<ClaimOpenJobHandler>();

        // Live tracking (SignalR hub + stale-cleanup worker both call this).
        services.AddScoped<Tracking.TrackingService>();
        services.AddScoped<Tracking.GetRouteHandler>();

        // Payments + wallet handlers.
        services.AddScoped<InitializePaymentHandler>();
        services.AddScoped<HandlePaymentWebhookHandler>();
        services.AddScoped<Payments.HandleTransferWebhookHandler>();
        services.AddScoped<Payments.GetBanksHandler>();
        services.AddScoped<RefundService>();
        services.AddScoped<GetWalletHandler>();
        services.AddScoped<GetWalletTransactionsHandler>();

        // Payouts (shared owner-agnostic engine + artisan earnings entry points).
        services.AddScoped<WithdrawalService>();
        services.AddScoped<GetArtisanWalletHandler>();
        services.AddScoped<Payments.CashCommissionService>();
        services.AddScoped<Payments.ArtisanStandingService>();
        services.AddScoped<Payments.SettleCommissionHandler>();
        services.AddScoped<GetArtisanWithdrawalsHandler>();
        services.AddScoped<RequestWithdrawalHandler>();

        // Reviews (customer rates a completed booking; public profile list).
        services.AddScoped<SubmitReviewHandler>();
        services.AddScoped<GetBookingReviewHandler>();
        services.AddScoped<GetArtisanReviewsHandler>();

        // Referrals (attribution at signup, reward on first completed job, dashboard).
        services.AddScoped<Referrals.ReferralService>();
        services.AddScoped<Referrals.GetMyReferralsHandler>();
        services.AddScoped<Referrals.RequestReferralWithdrawalHandler>();

        // Chat (per-booking conversations: send, thread, conversations list, unread).
        services.AddScoped<Chat.ChatService>();

        // Favourites (customer's saved artisans).
        services.AddScoped<Favorites.AddFavoriteHandler>();
        services.AddScoped<Favorites.RemoveFavoriteHandler>();
        services.AddScoped<Favorites.GetFavoritesHandler>();

        // Disputes (customer raises + views; admin lists/reviews/resolves).
        services.AddScoped<Disputes.RaiseDisputeHandler>();
        services.AddScoped<Disputes.GetBookingDisputeHandler>();
        services.AddScoped<Disputes.ListDisputesHandler>();
        services.AddScoped<Disputes.GetDisputeHandler>();
        services.AddScoped<Disputes.MarkDisputeUnderReviewHandler>();
        services.AddScoped<Disputes.ResolveDisputeHandler>();

        // Notifications (in-app feed + unread badge; emitter fans out domain events).
        services.AddScoped<Notifications.NotificationEmitter>();
        services.AddScoped<Notifications.GetNotificationsHandler>();
        services.AddScoped<Notifications.GetUnreadCountHandler>();
        services.AddScoped<Notifications.MarkNotificationReadHandler>();
        services.AddScoped<Notifications.MarkAllNotificationsReadHandler>();
        services.AddScoped<Notifications.RegisterPushTokenHandler>();
        services.AddScoped<Notifications.RemovePushTokenHandler>();

        // Platform settings (admin-controlled business values, read at runtime).
        services.AddScoped<Settings.GetPlatformSettingsHandler>();
        services.AddScoped<Settings.UpdatePlatformSettingsHandler>();

        // Admin operations (KYC verification, users, bookings, categories).
        services.AddScoped<Admin.ListKycSubmissionsHandler>();
        services.AddScoped<Admin.GetKycSubmissionHandler>();
        services.AddScoped<Admin.ReviewKycHandler>();
        services.AddScoped<Admin.ListUsersHandler>();
        services.AddScoped<Admin.SetUserSuspendedHandler>();
        services.AddScoped<Admin.ListAllBookingsHandler>();
        services.AddScoped<Admin.GetAdminBookingHandler>();
        services.AddScoped<Admin.GetAdminPaymentsHandler>();
        services.AddScoped<Admin.ListAdminArtisansHandler>();
        services.AddScoped<Admin.GetAdminArtisanCertificateHandler>();
        services.AddScoped<Admin.ListAllCategoriesHandler>();
        services.AddScoped<Admin.CreateCategoryHandler>();
        services.AddScoped<Admin.UpdateCategoryHandler>();
        services.AddScoped<Admin.SetCategoryActiveHandler>();
        return services;
    }
}
