namespace Servika.Domain.Payments;

/// <summary>
/// A request to cash out a Servika wallet balance to a bank account. The
/// <b>balance is always the append-only wallet ledger</b> — this entity only tracks
/// the disbursement's lifecycle (pending → paid/failed) and the destination. On
/// request a ledger debit (<see cref="WalletTransactionType.PayoutRequest"/>)
/// reserves the funds; a failed disbursement reverses that debit.
///
/// <para>Keyed to a ledger owner (<see cref="OwnerType"/> + <see cref="OwnerId"/>,
/// the same pair the wallet ledger uses) plus the requesting <see cref="UserId"/>.
/// An artisan payout is <c>(Artisan, profileId)</c>; a referral cash-out is
/// <c>(Referrer, userId)</c> — one entity, one set of payout rails.</para>
/// </summary>
public sealed class Withdrawal
{
    public Guid Id { get; private set; }

    /// <summary>The wallet ledger owner the funds are drawn from.</summary>
    public WalletOwnerType OwnerType { get; private set; }

    /// <summary>The owner id within <see cref="OwnerType"/> (artisan profile id or referrer user id).</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>The login account that requested the payout.</summary>
    public Guid UserId { get; private set; }

    public int AmountNaira { get; private set; }

    public WithdrawalStatus Status { get; private set; }

    /// <summary>Payout method, e.g. "bank".</summary>
    public string Method { get; private set; } = "bank";

    public string BankName { get; private set; } = string.Empty;

    /// <summary>Destination account, stored masked (last 4), e.g. "••••1234".</summary>
    public string AccountNumberMasked { get; private set; } = string.Empty;

    public string AccountName { get; private set; } = string.Empty;

    /// <summary>Payout provider slug (e.g. "stub" / "paystack").</summary>
    public string? Provider { get; private set; }

    /// <summary>Provider's transfer reference, once disbursed.</summary>
    public string? ProviderReference { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    private Withdrawal() { }

    public static Withdrawal Request(
        WalletOwnerType ownerType,
        Guid ownerId,
        Guid userId,
        int amountNaira,
        string bankName,
        string accountNumber,
        string accountName,
        DateTimeOffset now)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (amountNaira <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amountNaira));

        return new Withdrawal
        {
            Id = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            UserId = userId,
            AmountNaira = amountNaira,
            Status = WithdrawalStatus.Pending,
            Method = "bank",
            BankName = bankName?.Trim() ?? string.Empty,
            AccountNumberMasked = Mask(accountNumber),
            AccountName = accountName?.Trim() ?? string.Empty,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Records that a real transfer was initiated with the provider but hasn't
    /// settled yet — the payout stays <see cref="WithdrawalStatus.Pending"/> until
    /// the provider's transfer webhook resolves it (async providers like Paystack
    /// Transfers). Stamps the provider + its transfer code for correlation.
    /// </summary>
    public void BeginProcessing(string provider, string? providerReference)
    {
        if (Status != WithdrawalStatus.Pending) return;
        Provider = provider;
        ProviderReference = providerReference;
    }

    /// <summary>Marks the payout disbursed with the provider's reference.</summary>
    public void MarkPaid(string provider, string? providerReference, DateTimeOffset now)
    {
        if (Status != WithdrawalStatus.Pending) return;
        Status = WithdrawalStatus.Paid;
        Provider = provider;
        // Keep the transfer code stamped at initiate if the webhook omits it.
        ProviderReference = providerReference ?? ProviderReference;
        ProcessedAtUtc = now;
    }

    /// <summary>Marks the payout failed (the caller reverses the ledger debit).</summary>
    public void MarkFailed(string provider, string? reason, DateTimeOffset now)
    {
        if (Status != WithdrawalStatus.Pending) return;
        Status = WithdrawalStatus.Failed;
        Provider = provider;
        FailureReason = reason;
        ProcessedAtUtc = now;
    }

    private static string Mask(string? accountNumber)
    {
        var digits = new string((accountNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? digits : $"••••{digits[^4..]}";
    }
}
