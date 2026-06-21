using Microsoft.Extensions.DependencyInjection;
using Servika.Application.Catalogue;
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
        services.AddScoped<VerifyOtpHandler>();
        services.AddScoped<ResendOtpHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();

        // Catalogue (marketplace) query handlers.
        services.AddScoped<GetCategoriesHandler>();
        services.AddScoped<GetArtisansHandler>();
        services.AddScoped<GetArtisanByIdHandler>();
        return services;
    }
}
