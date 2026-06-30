namespace Servika.Domain.Payments;

/// <summary>
/// An append-only ledger entry (PRD: "wallet transactions must be append-only").
/// Each row is one immutable money-movement fact scoped to an owner; a balance is
/// always computed as the sum of an owner's <see cref="AmountNaira"/> — never
/// trusted from a client. <see cref="AmountNaira"/> is signed from the owner's
/// perspective: a credit is positive, a debit negative.
/// </summary>
public sealed class WalletTransaction
{
    /// <summary>Fixed owner id for Servika's own (platform) ledger.</summary>
    public static readonly Guid PlatformOwnerId =
        new("11111111-1111-1111-1111-111111111111");

    public Guid Id { get; private set; }
    public WalletOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public WalletTransactionType Type { get; private set; }

    /// <summary>Signed amount in Naira (credit +, debit −) from the owner's view.</summary>
    public int AmountNaira { get; private set; }

    public Guid? BookingId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private WalletTransaction() { }

    public static WalletTransaction Create(
        WalletOwnerType ownerType,
        Guid ownerId,
        WalletTransactionType type,
        int amountNaira,
        Guid? bookingId,
        Guid? paymentId,
        string description,
        DateTimeOffset now)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner is required.", nameof(ownerId));

        return new WalletTransaction
        {
            Id = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            Type = type,
            AmountNaira = amountNaira,
            BookingId = bookingId,
            PaymentId = paymentId,
            Description = description,
            CreatedAt = now,
        };
    }
}
