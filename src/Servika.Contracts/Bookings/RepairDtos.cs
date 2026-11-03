namespace Servika.Contracts.Bookings;

/// <summary>One repair the artisan picked from Servika's add-on list, with a photo of the fault.</summary>
public sealed record ProposeRepairItem(
    Guid PriceBookItemId,
    /// <summary>Photo of the fault (base64 or a data: URI). Required.</summary>
    string PhotoBase64,
    /// <summary>Parts for this repair, itemised beside Servika's labour price. 0 when none.</summary>
    int PartsNaira = 0,
    /// <summary>Which part and why, required when there is a parts cost.</summary>
    string? PartsNote = null);

/// <summary>The artisan's repairs after looking at the job (POST /artisan/jobs/{id}/repairs).</summary>
public sealed record ProposeRepairsRequest(List<ProposeRepairItem> Items);

/// <summary>The customer's answer (POST /bookings/{id}/repairs/decision): the repairs to go ahead with.
/// Every other proposed repair is declined. An empty list on a diagnosis visit ends the job there.</summary>
public sealed record DecideRepairsRequest(List<Guid> ApprovedRepairIds);

public sealed record BookingRepairDto(
    Guid Id,
    Guid PriceBookItemId,
    int Round,
    string Name,
    int LabourNaira,
    int PartsNaira,
    string? PartsNote,
    int TotalNaira,
    /// <summary>API path of the fault photo (participants only).</summary>
    string PhotoUrl,
    /// <summary>Proposed | Approved | Declined.</summary>
    string Status,
    DateTimeOffset ProposedAtUtc);

/// <summary>Everything about the repairs on one job, and what the money looks like now.</summary>
public sealed record BookingRepairsDto(
    IReadOnlyList<BookingRepairDto> Repairs,
    /// <summary>The diagnosis fee paid at booking (credited against the repairs), or null.</summary>
    int? DiagnosisFeeNaira,
    int ApprovedRepairsNaira,
    /// <summary>The agreed total for the whole job so far.</summary>
    int AgreedTotalNaira,
    int PaidNaira,
    int AmountDueNaira);
