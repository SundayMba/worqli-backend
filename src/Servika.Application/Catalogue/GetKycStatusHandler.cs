using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The signed-in artisan's KYC status. No submission yet → a "NotSubmitted" DTO
/// (the Pro app uses it to route into the verification flow).
/// </summary>
public sealed class GetKycStatusHandler
{
    private readonly IVerificationEventRepository _events;
    private readonly IArtisanKycRepository _kyc;

    public GetKycStatusHandler(IArtisanKycRepository kyc, IVerificationEventRepository events)
    {
        _events = events;
        _kyc = kyc;
    }

    public async Task<KycStatusDto> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var submission = await _kyc.GetForUserAsync(artisanUserId, ct);
        return submission is null
            ? new KycStatusDto("NotSubmitted", null, null, null, null, null)
            : submission.ToStatusDto((await _events.ListForKycAsync(submission.Id, ct)).ToArtisanDtos());
    }
}
