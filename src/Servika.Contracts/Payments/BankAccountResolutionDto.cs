namespace Servika.Contracts.Payments;

/// <summary>GET /api/v1/banks/resolve: the name the bank holds for an account number.</summary>
public sealed record BankAccountResolutionDto(string BankCode, string AccountNumber, string AccountName);
