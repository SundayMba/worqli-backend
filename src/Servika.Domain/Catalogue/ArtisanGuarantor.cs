namespace Servika.Domain.Catalogue;

/// <summary>What happened when a reviewer called the guarantor.</summary>
public enum GuarantorContactStatus
{
    NotCalled,
    /// <summary>Reached them and they vouch for the artisan.</summary>
    Confirmed,
    /// <summary>No answer, switched off or wrong number.</summary>
    Unreachable,
    /// <summary>Reached them and they do not know or will not vouch for the artisan.</summary>
    Denied,
}

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

    /// <summary>The reviewer's call outcome. A guarantor the artisan replaces is a new row, so it starts over.</summary>
    public GuarantorContactStatus ContactStatus { get; private set; } = GuarantorContactStatus.NotCalled;
    public string? ContactNote { get; private set; }
    public DateTimeOffset? ContactedAtUtc { get; private set; }
    public Guid? ContactedByUserId { get; private set; }

    /// <summary>The artisan corrects a guarantor (e.g. after a reviewer asked). A new
    /// ID photo replaces the old one; null keeps it. If the person or number changed,
    /// any earlier call record no longer applies, so it starts over.</summary>
    public void Update(string fullName, string phone, string relationship, int yearsKnown,
        string? occupation, string? address, string? newIdPhotoKey)
    {
        var fresh = Create(ArtisanUserId, fullName, phone, relationship, yearsKnown, occupation, address,
            newIdPhotoKey ?? IdPhotoKey, CreatedAt);
        var personChanged = NormalizePhone(fresh.Phone) != NormalizePhone(Phone)
            || !string.Equals(fresh.FullName, FullName, StringComparison.OrdinalIgnoreCase);
        FullName = fresh.FullName;
        Phone = fresh.Phone;
        Relationship = fresh.Relationship;
        YearsKnown = fresh.YearsKnown;
        Occupation = fresh.Occupation;
        Address = fresh.Address;
        IdPhotoKey = fresh.IdPhotoKey;
        if (personChanged)
        {
            ContactStatus = GuarantorContactStatus.NotCalled;
            ContactNote = null;
            ContactedAtUtc = null;
            ContactedByUserId = null;
        }
    }

    public void RecordContact(GuarantorContactStatus status, string? note, Guid adminUserId, DateTimeOffset now)
    {
        ContactStatus = status;
        ContactNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 300)];
        ContactedAtUtc = status == GuarantorContactStatus.NotCalled ? null : now;
        ContactedByUserId = status == GuarantorContactStatus.NotCalled ? null : adminUserId;
    }

    /// <summary>One shape for a Nigerian number so "+234 803…", "234803…" and "0803…" compare equal.</summary>
    public static string NormalizePhone(string? phone)
    {
        var d = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (d.StartsWith("234") && d.Length == 13) return "0" + d[3..];
        if (d.Length == 10 && !d.StartsWith('0')) return "0" + d;
        return d;
    }

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
