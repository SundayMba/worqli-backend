using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

internal static class EstateEnquiryMapping
{
    public static EstateEnquiryDto ToDto(this EstateEnquiry e) =>
        new(e.Id, e.OrganisationName, e.ContactName, e.ContactRole, e.Phone, e.Email, e.Location, e.UnitCount,
            e.ServicesWanted, e.Frequency, e.Notes, e.Status.ToString(), e.AdminNote, e.CreatedAtUtc, e.UpdatedAtUtc);
}

/// <summary>An estate or facility manager asks to talk: stored for ops, who are alerted at once.</summary>
public sealed class SubmitEstateEnquiryHandler
{
    private readonly IEstateEnquiryRepository _enquiries;
    private readonly ICatalogueRepository _catalogue;
    private readonly IAdminAlerts _alerts;
    private readonly IClock _clock;

    public SubmitEstateEnquiryHandler(IEstateEnquiryRepository enquiries, ICatalogueRepository catalogue, IAdminAlerts alerts, IClock clock)
    {
        _enquiries = enquiries;
        _catalogue = catalogue;
        _alerts = alerts;
        _clock = clock;
    }

    public async Task<EstateEnquiryDto> HandleAsync(Guid? userId, SubmitEstateEnquiryRequest r, CancellationToken ct)
    {
        var names = (await _catalogue.GetAllCategoriesAsync(ct)).ToDictionary(c => c.Slug, c => c.Name);
        var unknown = (r.ServicesWanted ?? new()).Where(s => !names.ContainsKey(s.Trim().ToLowerInvariant())).ToList();
        if (unknown.Count > 0) throw new ArgumentException("One of the services picked is not offered.");

        var e = EstateEnquiry.Submit(r.OrganisationName, r.ContactName, r.ContactRole, r.Phone, r.Email, r.Location,
            r.UnitCount, r.ServicesWanted, r.Frequency, r.Notes, userId, _clock.UtcNow);
        _enquiries.Add(e);
        await _enquiries.SaveChangesAsync(ct);

        // Names, area and what they want only: no phone numbers in the alert channels.
        _alerts.Send(new AdminAlert(AdminAlertKind.EstateEnquiry, "An estate wants to talk",
            new[]
            {
                $"{e.OrganisationName} in {e.Location}{(e.UnitCount is { } u ? $", about {u} units" : "")}.",
                $"Wants: {string.Join(", ", e.ServicesWanted.Select(s => names.GetValueOrDefault(s, s)))}. {e.Frequency}.",
            },
            "/estates", "Open the enquiry"));
        return e.ToDto();
    }
}

/// <summary>Estate enquiries for ops, new ones first.</summary>
public sealed class ListEstateEnquiriesHandler
{
    private readonly IEstateEnquiryRepository _enquiries;

    public ListEstateEnquiriesHandler(IEstateEnquiryRepository enquiries) => _enquiries = enquiries;

    public async Task<IReadOnlyList<EstateEnquiryDto>> HandleAsync(CancellationToken ct) =>
        (await _enquiries.ListAsync(ct))
            .OrderBy(e => e.Status)
            .ThenByDescending(e => e.CreatedAtUtc)
            .Select(e => e.ToDto())
            .ToList();
}

/// <summary>Ops record where the follow-up is (New → Contacted → Closed) and a note.</summary>
public sealed class UpdateEstateEnquiryHandler
{
    private readonly IEstateEnquiryRepository _enquiries;
    private readonly IClock _clock;

    public UpdateEstateEnquiryHandler(IEstateEnquiryRepository enquiries, IClock clock)
    {
        _enquiries = enquiries;
        _clock = clock;
    }

    public async Task<EstateEnquiryDto> HandleAsync(Guid adminUserId, Guid id, UpdateEstateEnquiryRequest r, CancellationToken ct)
    {
        var e = await _enquiries.FindForUpdateAsync(id, ct) ?? throw new NotFoundException("That enquiry was not found.");
        if (!Enum.TryParse<EstateEnquiryStatus>(r.Status, true, out var status))
            throw new ArgumentException("Status must be New, Contacted or Closed.");
        e.Update(status, r.AdminNote, adminUserId, _clock.UtcNow);
        await _enquiries.SaveChangesAsync(ct);
        return e.ToDto();
    }
}
