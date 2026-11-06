namespace Servika.Domain.Bookings;

/// <summary>
/// One material/parts line on an itemised quote ("2 × 13A socket @ ₦1,500").
/// Stored as JSON on the bid. Immutable; a revision replaces the whole list.
/// </summary>
public sealed record BidMaterialLine(string Name, int Quantity, int UnitPriceNaira)
{
    public const int MaxNameLength = 80;
    public const int MaxLines = 20;
    /// <summary>Upper bound for one line and for labour, so totals of 20 lines always fit an int.</summary>
    public const int MaxLineNaira = 50_000_000;

    public int TotalNaira => Quantity * UnitPriceNaira;

    /// <summary>Trims and validates one line; throws on an unusable entry.</summary>
    public static BidMaterialLine Create(string name, int quantity, int unitPriceNaira)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0)
            throw new ArgumentException("Each material needs a name.", nameof(name));
        if (n.Length > MaxNameLength) n = n[..MaxNameLength];
        if (quantity < 1)
            throw new ArgumentException("Material quantity must be at least 1.", nameof(quantity));
        if (unitPriceNaira < 0)
            throw new ArgumentException("Material price can't be negative.", nameof(unitPriceNaira));
        if (quantity > 10_000 || (long)quantity * unitPriceNaira > MaxLineNaira)
            throw new ArgumentException("That material line is too large. Check the quantity and price.", nameof(quantity));
        return new BidMaterialLine(n, quantity, unitPriceNaira);
    }
}
