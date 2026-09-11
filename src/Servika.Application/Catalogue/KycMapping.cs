using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>Maps a KYC submission to its status DTO (image keys never leave here).</summary>
internal static class KycMapping
{
    public static KycStatusDto ToStatusDto(this ArtisanKyc k, IReadOnlyList<VerificationEventDto>? events = null) =>
        new(k.Status.ToString(), k.IdType.ToString(), k.IdNumber, k.ReviewNote,
            k.SubmittedAtUtc, k.ReviewedAtUtc,
            k.OpenCheck?.ToString(), k.OpenReasonCode, k.OpenNote, k.ResubmissionCount, events);
}
