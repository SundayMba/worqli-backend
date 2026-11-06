using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// What an artisan may see of a request they have not been picked for: the area, a point rounded to about a
/// kilometre (enough for distance), and the customer's first name only once their own ID has been checked. The
/// street, gate notes and exact pin come after the customer picks them. Anyone can sign up as an artisan, so the
/// open pool must never be a list of strangers' home addresses.
/// </summary>
public static class OpenJobPrivacy
{
    public static string Area(string? address)
    {
        var parts = (address ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1) return string.Join(", ", parts.Skip(1));
        if (parts.Length == 1 && !parts[0].Any(char.IsDigit)) return parts[0];
        return "Area shown once you are picked";
    }

    private static double? Coarse(double? v) => v is { } x ? Math.Round(x, 2) : null;

    private static string? FirstName(string? name, bool verified) =>
        verified && !string.IsNullOrWhiteSpace(name) ? name.Trim().Split(' ')[0] : null;

    public static BookingSummaryDto Coarsen(BookingSummaryDto b) =>
        b with { AddressText = Area(b.AddressText), LocationLat = Coarse(b.LocationLat), LocationLng = Coarse(b.LocationLng) };

    public static BookingDetailDto Coarsen(BookingDetailDto b, bool verified) =>
        b with
        {
            AddressText = Area(b.AddressText),
            LocationLat = Coarse(b.LocationLat),
            LocationLng = Coarse(b.LocationLng),
            LocationInstructions = null,
            CustomerName = FirstName(b.CustomerName, verified),
        };
}
