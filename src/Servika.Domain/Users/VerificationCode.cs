namespace Servika.Domain.Users;

/// <summary>
/// A one-time code (numeric OTP) or reset token issued to a user for a specific
/// purpose. Only a deterministic hash of the secret is stored, never the raw
/// value. It is single-use, expires quickly, and tolerates a limited number of
/// wrong attempts before it locks.
/// </summary>
public sealed class VerificationCode
{
    /// <summary>Max wrong attempts before the code is considered burned.</summary>
    public const int MaxAttempts = 5;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public OtpPurpose Purpose { get; private set; }

    /// <summary>Deterministic (SHA-256) hash of the code — searchable, not reversible.</summary>
    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public int Attempts { get; private set; }

    /// <summary>True while unused, not expired, and under the attempt limit.</summary>
    public bool IsRedeemable(DateTimeOffset nowUtc) =>
        ConsumedAtUtc is null && Attempts < MaxAttempts && nowUtc < ExpiresAtUtc;

    private VerificationCode() { }

    private VerificationCode(
        Guid id, Guid userId, OtpPurpose purpose, string codeHash,
        DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Purpose = purpose;
        CodeHash = codeHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public static VerificationCode Issue(
        Guid userId, OtpPurpose purpose, string codeHash,
        DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException("Code hash is required.", nameof(codeHash));

        return new VerificationCode(Guid.NewGuid(), userId, purpose, codeHash, expiresAtUtc, createdAtUtc);
    }

    /// <summary>Records a wrong guess, so repeated failures eventually lock the code.</summary>
    public void RegisterFailedAttempt() => Attempts++;

    /// <summary>Marks the code as used; it can never be redeemed again.</summary>
    public void Consume(DateTimeOffset whenUtc) => ConsumedAtUtc = whenUtc;
}
