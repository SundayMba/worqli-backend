using Servika.Application.Abstractions.Kyc;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The artisan checks their NIN against the register from the identity screen. The
/// outcome is stored on the profile for the reviewer; approval stays a person's call.
/// Lookups cost money, so three a day per artisan.
/// </summary>
public sealed class VerifyNinHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly INinLookupProvider _provider;
    private readonly IClock _clock;

    public VerifyNinHandler(ICatalogueRepository catalogue, IUserRepository users, INinLookupProvider provider, IClock clock)
    {
        _catalogue = catalogue;
        _users = users;
        _provider = provider;
        _clock = clock;
    }

    public async Task<NinVerifyResultDto> HandleAsync(Guid artisanUserId, VerifyNinRequest request, CancellationToken ct)
    {
        var nin = (request.Nin ?? string.Empty).Replace(" ", "");
        if (nin.Length != 11 || !nin.All(char.IsDigit))
            throw new ArgumentException("A NIN is eleven digits.");

        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Set up your trade and work area before checking your NIN.");
        var user = await _users.FindByIdAsync(artisanUserId, ct);
        var now = _clock.UtcNow;

        if (!_provider.IsConfigured)
            return new NinVerifyResultDto("Unavailable", null, profile.NinLookupsLeft(now));

        var left = profile.NinLookupsLeft(now);
        if (left <= 0)
            throw new TooManyRequestsException("Three checks a day is the limit. Upload a photo of your NIN slip and the reviewer will check it.");

        string status;
        string? registerName = null;
        try
        {
            var found = await _provider.LookupAsync(nin, user?.FullName, ct);
            if (found is null)
            {
                status = "NotFound";
            }
            else
            {
                registerName = string.Join(' ', new[] { found.LastName, found.FirstName, found.MiddleName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                status = NamesMatch(user?.FullName, found) ? "Matched" : "NameMismatch";
            }
        }
        catch (Exception)
        {
            status = "Failed";
        }

        profile.RecordNinLookup(nin, status, registerName, now);
        await _catalogue.SaveChangesAsync(ct);

        return new NinVerifyResultDto(status, Mask(registerName), profile.NinLookupsLeft(now));
    }

    /// <summary>Two of the account's name tokens appear in the register name (order and middle names vary).</summary>
    internal static bool NamesMatch(string? accountName, NinLookupResult found)
    {
        if (string.IsNullOrWhiteSpace(accountName)) return false;
        var register = new HashSet<string>(
            new[] { found.FirstName, found.LastName, found.MiddleName ?? "" }
                .SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(Norm));
        var account = accountName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Norm).Where(t => t.Length >= 2).ToList();
        var hits = account.Count(register.Contains);
        return hits >= Math.Min(2, account.Count);
    }

    private static string Norm(string s) => new(s.ToLowerInvariant().Where(char.IsLetter).ToArray());

    /// <summary>"IKOT Ukeme Sunday" → "IKOT U. S." so the screen confirms without spelling the whole record out.</summary>
    private static string? Mask(string? registerName)
    {
        if (string.IsNullOrWhiteSpace(registerName)) return null;
        var parts = registerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].ToUpperInvariant();
        return parts[0].ToUpperInvariant() + " " + string.Join(' ', parts.Skip(1).Select(p => p[0].ToString().ToUpperInvariant() + "."));
    }
}
