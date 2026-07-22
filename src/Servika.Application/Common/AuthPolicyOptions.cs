namespace Servika.Application.Common;

/// <summary>
/// Auth/verification policy toggles, bound from the <c>Auth</c> config section.
/// Kept off by default so enabling a gate is a config flip once the apps prompt
/// for it — no code change.
/// </summary>
public sealed class AuthPolicyOptions
{
    public const string SectionName = "Auth";

    /// <summary>When true, a customer must have a verified phone before they can
    /// create a booking or open a chat with an artisan. Default false.</summary>
    public bool RequirePhoneForBooking { get; init; }
}
