using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servika.Application.Abstractions.Security;
using Servika.Infrastructure.Persistence;
using Servika.Infrastructure.Security;

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

        return services;
    }
}
