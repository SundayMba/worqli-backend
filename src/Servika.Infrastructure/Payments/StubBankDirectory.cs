using Servika.Application.Abstractions.Payments;

namespace Servika.Infrastructure.Payments;

/// <summary>
/// A small static list of major Nigerian banks with Paystack (NIP) codes — used
/// when Paystack isn't configured so the withdrawal bank picker works locally.
/// The real list comes from <see cref="PaystackBankDirectory"/> (Paystack /bank).
/// </summary>
public sealed class StubBankDirectory : IBankDirectory
{
    private static readonly IReadOnlyList<BankInfo> Banks = new[]
    {
        new BankInfo("Access Bank", "044"),
        new BankInfo("Guaranty Trust Bank (GTBank)", "058"),
        new BankInfo("Zenith Bank", "057"),
        new BankInfo("First Bank of Nigeria", "011"),
        new BankInfo("United Bank for Africa (UBA)", "033"),
        new BankInfo("Fidelity Bank", "070"),
        new BankInfo("Union Bank", "032"),
        new BankInfo("Stanbic IBTC Bank", "221"),
        new BankInfo("Sterling Bank", "232"),
        new BankInfo("Wema Bank", "035"),
        new BankInfo("Ecobank Nigeria", "050"),
        new BankInfo("First City Monument Bank (FCMB)", "214"),
        new BankInfo("Polaris Bank", "076"),
        new BankInfo("Keystone Bank", "082"),
        new BankInfo("Kuda Microfinance Bank", "50211"),
        new BankInfo("Opay", "999992"),
        new BankInfo("PalmPay", "999991"),
        new BankInfo("Moniepoint MFB", "50515"),
    };

    public Task<IReadOnlyList<BankInfo>> ListBanksAsync(CancellationToken ct) =>
        Task.FromResult(Banks);
}
