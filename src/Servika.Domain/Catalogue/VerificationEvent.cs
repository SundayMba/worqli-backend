namespace Servika.Domain.Catalogue;

/// <summary>The six things a reviewer looks at. Mirrors the checklist in the Pro app.</summary>
public enum VerificationCheck
{
    Trade,
    Photo,
    Identity,
    Selfie,
    Guarantors,
    Payout,
    Documents,
}

public enum VerificationEventAction
{
    Submitted,
    Resubmitted,
    ChangesRequested,
    Approved,
    Declined,
}

/// <summary>
/// One line in an application's history: who did what, about which check, and what
/// they said. Append-only; the reviewer's exact words are what the artisan reads.
/// </summary>
public sealed class VerificationEvent
{
    public Guid Id { get; private set; }
    /// <summary>The KYC submission this belongs to.</summary>
    public Guid KycId { get; private set; }
    /// <summary>The artisan whose application it is.</summary>
    public Guid UserId { get; private set; }
    /// <summary>The admin who acted, or null when the artisan did (submit / resubmit).</summary>
    public Guid? ActorUserId { get; private set; }
    public VerificationEventAction Action { get; private set; }
    public VerificationCheck? Check { get; private set; }
    /// <summary>A short machine code for the reason (document_unreadable, name_mismatch, …).</summary>
    public string? ReasonCode { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private VerificationEvent() { }

    public static VerificationEvent Create(
        Guid kycId, Guid userId, Guid? actorUserId, VerificationEventAction action,
        VerificationCheck? check, string? reasonCode, string? note, DateTimeOffset now)
    {
        return new VerificationEvent
        {
            Id = Guid.NewGuid(),
            KycId = kycId,
            UserId = userId,
            ActorUserId = actorUserId,
            Action = action,
            Check = check,
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAtUtc = now,
        };
    }
}
