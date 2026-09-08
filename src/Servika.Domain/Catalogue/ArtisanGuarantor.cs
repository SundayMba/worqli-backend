namespace Servika.Domain.Catalogue;

/// <summary>
/// Someone who vouches for an artisan: a person Servika can call if something
/// goes wrong on a job, and only then. Part of the verification hub ("two
/// guarantors"); recommended at launch, not a hard gate. Holds the ID photo as a
/// storage key, never bytes.
/// </summary>
public sealed class ArtisanGuarantor
{
    public Guid Id { get; private set; }

    /// <summary>The artisan's login account (guarantors belong to the person,
    /// not the marketplace profile, so they survive profile edits).</summary>
    public Guid ArtisanUserId { get; private set; }

    public string FullName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    /// <summary>"Former employer" / "Landlord" / "Family" / "Community leader" / free text.</summary>
    public string Relationship { get; private set; } = string.Empty;
    public int YearsKnown { get; private set; }
    public string? Occupation { get; private set; }
    public string? Address { get; private set; }
    /// <summary>Storage key of the guarantor's ID photo, or null.</summary>
    public string? IdPhotoKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ArtisanGuarantor() { }

    public static ArtisanGuarantor Create(
        Guid artisanUserId,
        string fullName,
        string phone,
        string relationship,
        int yearsKnown,
        string? occupation,
        string? address,
        string? idPhotoKey,
        DateTimeOffset now)
    {
        if (artisanUserId == Guid.Empty)
            throw new ArgumentException("Artisan is required.", nameof(artisanUserId));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("The guarantor's full name is required.", nameof(fullName));
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
            throw new ArgumentException("Enter a reachable phone number for the guarantor.", nameof(phone));
        if (string.IsNullOrWhiteSpace(relationship))
            throw new ArgumentException("Say how you know this person.", nameof(relationship));

        return new ArtisanGuarantor
        {
            Id = Guid.NewGuid(),
            ArtisanUserId = artisanUserId,
            FullName = fullName.Trim(),
            Phone = phone!.Trim(),
            Relationship = relationship.Trim(),
            YearsKnown = Math.Max(0, yearsKnown),
            Occupation = string.IsNullOrWhiteSpace(occupation) ? null : occupation.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            IdPhotoKey = idPhotoKey,
            CreatedAt = now,
        };
    }
}
