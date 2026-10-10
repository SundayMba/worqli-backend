using System.Collections.Concurrent;
using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;

namespace Servika.Application.Payments;

/// <summary>
/// Bank-account name lookup with two guards around the billed provider call: a
/// process-wide cache per (bank, number) so retyping never bills twice, and a
/// per-user daily cap. Used at input time (payout screen) and again on save so the
/// stored name is always the bank's, never the artisan's typing.
/// </summary>
public sealed class BankAccountResolver
{
    public const int MaxLookupsPerUserPerDay = 10;
    private static readonly TimeSpan HitTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan MissTtl = TimeSpan.FromMinutes(30);

    private static readonly ConcurrentDictionary<string, (ResolvedBankAccount? Value, DateTimeOffset ExpiresAt)> Cache = new();
    private static readonly ConcurrentDictionary<string, int> DailyCounts = new();

    private readonly IBankDirectory _banks;
    private readonly IClock _clock;

    public BankAccountResolver(IBankDirectory banks, IClock clock)
    {
        _banks = banks;
        _clock = clock;
    }

    /// <summary>Null when the account does not exist at that bank. Throws <see cref="TooManyRequestsException"/> past the daily cap.</summary>
    public async Task<ResolvedBankAccount?> ResolveAsync(Guid userId, string bankCode, string accountNumber, string? hintName, CancellationToken ct)
    {
        var digits = new string((accountNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 10) throw new ArgumentException("A Nigerian account number has 10 digits.");
        if (string.IsNullOrWhiteSpace(bankCode)) throw new ArgumentException("Pick the bank first.");

        var now = _clock.UtcNow;
        if (TryCached(bankCode.Trim(), digits, now, out var cached)) return cached;
        CountLookup(userId, now);
        return await ResolveUncappedAsync(bankCode.Trim(), digits, hintName, ct);
    }

    /// <summary>
    /// "Type the number, see the banks" (the OPay flow). Counts as ONE lookup against the
    /// daily cap however many banks are asked. Candidates are narrowed first, so most
    /// numbers cost a handful of provider calls, and a provider only bills a hit.
    /// </summary>
    public async Task<IReadOnlyList<(BankInfo Bank, ResolvedBankAccount Account)>> FindAsync(
        Guid userId, string accountNumber, string? hintName, CancellationToken ct)
    {
        var digits = new string((accountNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 10) throw new ArgumentException("A Nigerian account number has 10 digits.");

        var now = _clock.UtcNow;
        var candidates = BankCandidates.For(digits, await _banks.ListBanksAsync(ct));
        if (!candidates.All(b => TryCached(b.Code, digits, now, out _))) CountLookup(userId, now);

        var found = new ConcurrentBag<(int Order, BankInfo Bank, ResolvedBankAccount Account)>();
        using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(candidates.Select(async (bank, i) =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var hit = TryCached(bank.Code, digits, now, out var c) ? c : await ResolveUncappedAsync(bank.Code, digits, hintName, ct);
                if (hit is not null) found.Add((i, bank, hit));
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // One bank timing out must not sink the whole search.
            }
            finally
            {
                gate.Release();
            }
        }));
        return found.OrderBy(f => f.Order).Select(f => (f.Bank, f.Account)).ToList();
    }

    private static bool TryCached(string bankCode, string digits, DateTimeOffset now, out ResolvedBankAccount? value)
    {
        if (Cache.TryGetValue($"{bankCode}|{digits}", out var hit) && hit.ExpiresAt > now)
        {
            value = hit.Value;
            return true;
        }
        value = null;
        return false;
    }

    private void CountLookup(Guid userId, DateTimeOffset now)
    {
        var used = DailyCounts.AddOrUpdate($"{userId:N}|{now.UtcDateTime:yyyyMMdd}", 1, (_, n) => n + 1);
        if (used > MaxLookupsPerUserPerDay)
            throw new TooManyRequestsException("That is enough account checks for today. Try again tomorrow, or contact support.");
    }

    private async Task<ResolvedBankAccount?> ResolveUncappedAsync(string bankCode, string digits, string? hintName, CancellationToken ct)
    {
        var result = await _banks.ResolveAccountAsync(bankCode, digits, hintName, ct);
        Cache[$"{bankCode}|{digits}"] = (result, _clock.UtcNow + (result is null ? MissTtl : HitTtl));
        return result;
    }
}

/// <summary>
/// Which banks could hold a 10-digit number. Commercial banks issue NUBAN numbers whose last
/// digit is a checksum over the CBN bank code, so most banks are ruled out without a call.
/// Wallet banks (OPay, PalmPay, Moniepoint, Kuda) often use the phone number as the account
/// number and do not follow that checksum, so they are always asked, first when the number
/// looks like a phone number.
/// </summary>
public static class BankCandidates
{
    public const int Max = 10;

    private static readonly string[] WalletCodes = { "999992", "999991", "50515", "50211" };
    private static readonly int[] Weights = { 3, 7, 3, 3, 7, 3, 3, 7, 3, 3, 7, 3, 3, 7, 3 };

    public static IReadOnlyList<BankInfo> For(string digits, IReadOnlyList<BankInfo> banks)
    {
        var wallets = WalletCodes
            .Select(code => banks.FirstOrDefault(b => b.Code == code))
            .Where(b => b is not null)
            .Cast<BankInfo>()
            .ToList();
        var nuban = banks.Where(b => b.Code.Length == 3 && b.Code.All(char.IsDigit) && PassesNuban(b.Code, digits)).ToList();

        var looksLikePhone = digits[0] is '7' or '8' or '9' && digits[1] is '0' or '1';
        var ordered = looksLikePhone ? wallets.Concat(nuban) : nuban.Concat(wallets);
        return ordered.DistinctBy(b => b.Code).Take(Max).ToList();
    }

    /// <summary>CBN NUBAN: weights 3,7,3 over "000" + 3-digit bank code + the 9-digit serial.</summary>
    public static bool PassesNuban(string bankCode, string digits)
    {
        var seed = "000" + bankCode + digits[..9];
        var sum = 0;
        for (var i = 0; i < 15; i++) sum += (seed[i] - '0') * Weights[i];
        var check = (10 - sum % 10) % 10;
        return check == digits[9] - '0';
    }
}

/// <summary>GET /api/v1/banks/lookup: every bank where a typed number exists, with the bank's name for it.</summary>
public sealed class FindBankAccountsHandler
{
    private readonly BankAccountResolver _resolver;
    private readonly Abstractions.Persistence.IUserRepository _users;

    public FindBankAccountsHandler(BankAccountResolver resolver, Abstractions.Persistence.IUserRepository users)
    {
        _resolver = resolver;
        _users = users;
    }

    public async Task<IReadOnlyList<Contracts.Payments.BankAccountMatchDto>> HandleAsync(Guid userId, string accountNumber, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        var found = await _resolver.FindAsync(userId, accountNumber, user?.FullName, ct);
        return found.Select(f => new Contracts.Payments.BankAccountMatchDto(f.Bank.Code, f.Bank.Name, f.Account.AccountNumber, f.Account.AccountName)).ToList();
    }
}

/// <summary>GET /api/v1/banks/resolve for the payout screens.</summary>
public sealed class ResolveBankAccountHandler
{
    private readonly BankAccountResolver _resolver;
    private readonly Abstractions.Persistence.IUserRepository _users;

    public ResolveBankAccountHandler(BankAccountResolver resolver, Abstractions.Persistence.IUserRepository users)
    {
        _resolver = resolver;
        _users = users;
    }

    public async Task<Contracts.Payments.BankAccountResolutionDto> HandleAsync(Guid userId, string bankCode, string accountNumber, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        var found = await _resolver.ResolveAsync(userId, bankCode, accountNumber, user?.FullName, ct)
            ?? throw new NotFoundException("No account with that number at this bank. Check the digits.");
        return new Contracts.Payments.BankAccountResolutionDto(found.BankCode, found.AccountNumber, found.AccountName);
    }
}
