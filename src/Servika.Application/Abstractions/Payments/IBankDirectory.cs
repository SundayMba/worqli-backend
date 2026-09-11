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

    /// <summary>
    /// Asks the bank (through the payout provider) whose name is on an account. Null when the
    /// account does not exist at that bank; throws when the provider cannot be reached.
    /// <paramref name="hintName"/> is only used by the dev stub to fabricate a plausible answer.
    /// </summary>
    Task<ResolvedBankAccount?> ResolveAccountAsync(string bankCode, string accountNumber, string? hintName, CancellationToken ct);
}

/// <summary>The bank's own record of an account: the name money to this number lands with.</summary>
public sealed record ResolvedBankAccount(string BankCode, string AccountNumber, string AccountName);

/// <summary>A payout-destination bank: display name + the provider's code.</summary>
public sealed record BankInfo(string Name, string Code);
