using Microsoft.Extensions.Configuration;
using Servika.Application.Abstractions.Verification;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Verification;

/// <summary>
/// Launch verification provider: a human reviews the submission, so it stays
/// <see cref="ArtisanVerificationStatus.Pending"/> until an admin decides. A
/// <c>Kyc:AutoApprove</c> flag (dev/CI) approves immediately so the onboarding →
/// bookable loop is testable before the admin slice exists. An automated provider
/// (NIN + liveness) implements the same port later.
/// </summary>
public sealed class ManualKycProvider : IKycVerificationProvider
{
    private readonly bool _autoApprove;

    public ManualKycProvider(IConfiguration configuration)
    {
        _autoApprove = configuration.GetValue<bool>("Kyc:AutoApprove");
    }

    public string Provider => "manual";

    public Task<ArtisanVerificationStatus> ReviewAsync(ArtisanKyc submission, CancellationToken ct) =>
        Task.FromResult(_autoApprove
            ? ArtisanVerificationStatus.Verified
            : ArtisanVerificationStatus.Pending);
}
