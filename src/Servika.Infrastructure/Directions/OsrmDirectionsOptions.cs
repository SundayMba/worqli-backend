namespace Servika.Infrastructure.Directions;

/// <summary>
/// An OSRM-compatible routing service: a self-hosted OSRM, or a hosted provider that
/// speaks the same <c>/route/v1/driving/{lng},{lat};{lng},{lat}</c> API (LocationIQ
/// Directions, Geoapify's OSRM mode, the OSRM demo server for development). Chosen
/// over Google when <c>Osrm:BaseUrl</c> is set. Keys live in config, never in the app.
/// </summary>
public sealed class OsrmDirectionsOptions
{
    public const string SectionName = "Osrm";

    /// <summary>e.g. https://router.project-osrm.org, https://us1.locationiq.com/v1/directions, http://osrm:5000</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Optional API key appended as a query parameter (LocationIQ: <c>key</c>).</summary>
    public string ApiKey { get; init; } = string.Empty;
    public string ApiKeyQueryParam { get; init; } = "key";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
