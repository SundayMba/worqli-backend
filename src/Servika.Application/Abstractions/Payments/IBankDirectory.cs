namespace Servika.Application.Abstractions.Payments;

/// <summary>
/// The list of banks a payout can target, with the provider's bank codes. Real
/// transfers need the code (not a free-text name) to build a recipient, so the
/// withdrawal UI picks from this list. Backed by Paystack's <c>/bank</c> when
/// configured, else a static Nigerian-bank fallback.
/// </summary>
public interface IBankDirectory
{
    Task<IReadOnlyList<BankInfo>> ListBanksAsync(CancellationToken ct);
}

/// <summary>A payout-destination bank: display name + the provider's code.</summary>
public sealed record BankInfo(string Name, string Code);
