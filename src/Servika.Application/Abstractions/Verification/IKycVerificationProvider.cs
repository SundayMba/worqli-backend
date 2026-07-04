using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Verification;

/// <summary>
/// Port that decides a KYC submission's outcome. The launch impl is <b>manual</b>
/// (a person reviews and Approves/Rejects, optionally auto-approving in dev); an
/// automated provider (NIN lookup + selfie liveness via a Nigerian KYC vendor) can
/// replace it later without touching the submit/onboarding use cases.
/// </summary>
public interface IKycVerificationProvider
{
    /// <summary>Provider slug recorded for audit, e.g. "manual" / "smileid".</summary>
    string Provider { get; }

    /// <summary>
    /// Reviews a freshly submitted KYC. Returns the decision now when it can
    /// (automated pass/fail, or dev auto-approve); returns <see cref="ArtisanVerificationStatus.Pending"/>
    /// when a human must review (the normal launch path).
    /// </summary>
    Task<ArtisanVerificationStatus> ReviewAsync(ArtisanKyc submission, CancellationToken ct);
}
