namespace Servika.Domain.Bookings;

/// <summary>
/// How a booking is priced (PRD §Pricing). Stored as a string.
/// <list type="bullet">
/// <item><c>Fixed</c> — Servika sets the price up front.</item>
/// <item><c>Variable</c> — artisan quotes after inspection, within Servika bands.</item>
/// <item><c>Hybrid</c> — deposit / call-out fee now, variable quote later.</item>
/// </list>
/// Most home-repair jobs are Variable, so it is the default for the happy-path
/// slice until per-category pricing config arrives with the payments slice.
/// </summary>
public enum PricingModel
{
    Fixed = 0,
    Variable = 1,
    Hybrid = 2,
}
