namespace Servika.Contracts.Catalogue;

/// <summary>POST /api/v1/estate-enquiries: an estate or facility manager says what they need. Guests may send it.</summary>
public sealed record SubmitEstateEnquiryRequest(
    string OrganisationName,
    string ContactName,
    string? ContactRole,
    string Phone,
    string? Email,
    string Location,
    int? UnitCount,
    List<string> ServicesWanted,
    string Frequency,
    string? Notes);

/// <summary>An estate enquiry as ops see it.</summary>
public sealed record EstateEnquiryDto(
    Guid Id,
    string OrganisationName,
    string ContactName,
    string? ContactRole,
    string Phone,
    string? Email,
    string Location,
    int? UnitCount,
    IReadOnlyList<string> ServicesWanted,
    string Frequency,
    string? Notes,
    /// <summary>New | Contacted | Closed.</summary>
    string Status,
    string? AdminNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>PUT /api/v1/admin/estate-enquiries/{id}: where the follow-up is, and ops' note.</summary>
public sealed record UpdateEstateEnquiryRequest(string Status, string? AdminNote);
