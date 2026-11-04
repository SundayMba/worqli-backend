namespace Servika.Domain.Catalogue;

public enum EstateEnquiryStatus { New = 0, Contacted = 1, Closed = 2 }

/// <summary>
/// An estate or facility manager telling Servika what they need (user decision 2026-11-04): which trades, how many
/// units, how often, and how to reach them. Ops follow up by phone or email and agree a contract; nothing is booked
/// automatically.
/// </summary>
public sealed class EstateEnquiry
{
    public const int MaxTextLength = 120;
    public const int MaxNotesLength = 1000;

    public Guid Id { get; private set; }
    public string OrganisationName { get; private set; } = string.Empty;
    public string ContactName { get; private set; } = string.Empty;
    /// <summary>"Estate manager", "Facility manager", "Residents' association"…</summary>
    public string? ContactRole { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string Location { get; private set; } = string.Empty;
    /// <summary>Homes, flats or offices covered, roughly.</summary>
    public int? UnitCount { get; private set; }
    /// <summary>Category slugs of the trades wanted.</summary>
    public List<string> ServicesWanted { get; private set; } = new();
    /// <summary>"One-off", "Monthly", "Quarterly" or "Not sure yet".</summary>
    public string Frequency { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? UserId { get; private set; }
    public EstateEnquiryStatus Status { get; private set; }
    public string? AdminNote { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private EstateEnquiry() { }

    public static EstateEnquiry Submit(
        string organisationName, string contactName, string? contactRole, string phone, string? email, string location,
        int? unitCount, IEnumerable<string>? servicesWanted, string frequency, string? notes, Guid? userId, DateTimeOffset now)
    {
        string Required(string? v, string what)
        {
            var t = v?.Trim() ?? string.Empty;
            if (t.Length == 0) throw new ArgumentException($"Add the {what}.");
            if (t.Length > MaxTextLength) throw new ArgumentException($"Keep the {what} under {MaxTextLength} characters.");
            return t;
        }
        string? Optional(string? v, int max)
        {
            var t = v?.Trim();
            if (string.IsNullOrEmpty(t)) return null;
            return t.Length > max ? t[..max] : t;
        }

        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 14) throw new ArgumentException("Add a phone number ops can call.");
        var mail = Optional(email, MaxTextLength);
        if (mail is not null && !mail.Contains('@')) throw new ArgumentException("That email address does not look right.");
        if (unitCount is < 1 or > 100_000) throw new ArgumentException("Units should be between 1 and 100,000.");
        var services = (servicesWanted ?? Enumerable.Empty<string>())
            .Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Distinct().Take(20).ToList();
        if (services.Count == 0) throw new ArgumentException("Pick at least one service you need.");

        return new EstateEnquiry
        {
            Id = Guid.NewGuid(),
            OrganisationName = Required(organisationName, "estate or company name"),
            ContactName = Required(contactName, "contact name"),
            ContactRole = Optional(contactRole, MaxTextLength),
            Phone = phone!.Trim(),
            Email = mail,
            Location = Required(location, "location"),
            UnitCount = unitCount,
            ServicesWanted = services,
            Frequency = Required(frequency, "how often"),
            Notes = Optional(notes, MaxNotesLength),
            UserId = userId,
            Status = EstateEnquiryStatus.New,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(EstateEnquiryStatus status, string? adminNote, Guid adminUserId, DateTimeOffset now)
    {
        Status = status;
        var note = adminNote?.Trim();
        AdminNote = string.IsNullOrEmpty(note) ? null : note.Length > MaxNotesLength ? note[..MaxNotesLength] : note;
        UpdatedByUserId = adminUserId;
        UpdatedAtUtc = now;
    }
}
