namespace Servika.Contracts.Bookings;

/// <summary>
/// The customer's choice of how to settle the agreed price:
/// "online" (escrow, the protected default) or "cash" (pay after service).
/// </summary>
public sealed record ChoosePaymentMethodRequest(string Method);
