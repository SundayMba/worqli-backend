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
        var key = $"{bankCode.Trim()}|{digits}";
        if (Cache.TryGetValue(key, out var hit) && hit.ExpiresAt > now) return hit.Value;

        var dayKey = $"{userId:N}|{now.UtcDateTime:yyyyMMdd}";
        var used = DailyCounts.AddOrUpdate(dayKey, 1, (_, n) => n + 1);
        if (used > MaxLookupsPerUserPerDay)
            throw new TooManyRequestsException("That is enough account checks for today. Try again tomorrow, or contact support.");

        var result = await _banks.ResolveAccountAsync(bankCode.Trim(), digits, hintName, ct);
        Cache[key] = (result, now + (result is null ? MissTtl : HitTtl));
        return result;
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
