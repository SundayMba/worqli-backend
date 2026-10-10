namespace Servika.Contracts.Payments;

/// <summary>GET /api/v1/banks/resolve: the name the bank holds for an account number.</summary>
public sealed record BankAccountResolutionDto(string BankCode, string AccountNumber, string AccountName);

/// <summary>A bank where the typed account number exists, with the name the bank holds for it.</summary>
public sealed record BankAccountMatchDto(string BankCode, string BankName, string AccountNumber, string AccountName);
