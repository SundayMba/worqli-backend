namespace Servika.Contracts.Catalogue;

/// <summary>PUT /api/v1/artisan/profile/payout-account — the saved bank account
/// withdrawals default to. Bank code comes from GET /api/v1/banks.</summary>
public sealed record SetPayoutAccountRequest(
    string BankCode,
    string BankName,
    string AccountNumber,
    string AccountName);

/// <summary>PUT /api/v1/artisan/profile/work-preferences — radius, emergency opt-in,
/// and working hours (a small JSON blob the apps own, e.g. per-weekday ranges).</summary>
public sealed record SetWorkPreferencesRequest(
    int RadiusKm,
    bool AcceptsEmergency,
    string? WorkingHoursJson);

/// <summary>PUT /api/v1/artisan/profile/away — hide the profile until a date, or
/// null to come back. Accepted jobs are never moved by this.</summary>
public sealed record SetAwayRequest(DateTimeOffset? UntilUtc);

/// <summary>A guarantor as the artisan sees it (GET /api/v1/artisan/guarantors).</summary>
public sealed record GuarantorDto(
    Guid Id,
    string FullName,
    string Phone,
    string Relationship,
    int YearsKnown,
    string? Occupation,
    string? Address,
    bool HasIdPhoto,
    DateTimeOffset CreatedAt);

/// <summary>POST /api/v1/artisan/guarantors — add someone who vouches for you.
/// <see cref="IdPhotoBase64"/> is raw base64 or a data: URI, optional.</summary>
public sealed record AddGuarantorRequest(
    string FullName,
    string Phone,
    string Relationship,
    int YearsKnown,
    string? Occupation,
    string? Address,
    string? IdPhotoBase64);
