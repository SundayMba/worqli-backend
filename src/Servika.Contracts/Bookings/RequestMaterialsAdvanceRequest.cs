namespace Servika.Contracts.Bookings;

/// <summary>POST /api/v1/artisan/jobs/{id}/materials-advance body.</summary>
public sealed record RequestMaterialsAdvanceRequest(int AmountNaira);
