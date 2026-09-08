namespace Servika.Contracts.Auth;

/// <summary>
/// The body the mobile app sends to POST /api/v1/auth/register. A plain data
/// shape (DTO) — no logic, no secrets beyond the password the user typed.
/// </summary>
/// <param name="FullName">The user's full name.</param>
/// <param name="Email">Email address; must be unique. Normalised server-side.</param>
/// <param name="PhoneNumber">Optional contact phone number. Customers are not asked at signup; the phone is verified at point of use.</param>
/// <param name="Password">Plain password (min 8 chars). Hashed server-side, never stored raw.</param>
/// <param name="Role">Optional. "Customer" (default) or "Artisan". Admin roles are never self-registered.</param>
/// <param name="ReferralCode">Optional. A referrer's share code — links this signup to them.</param>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber = null,
    string? Role = null,
    string? ReferralCode = null);
