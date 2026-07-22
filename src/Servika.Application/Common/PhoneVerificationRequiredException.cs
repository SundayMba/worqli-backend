namespace Servika.Application.Common;

/// <summary>
/// Thrown when an action that needs a reachable phone (booking, contacting an
/// artisan) is attempted by a customer whose phone isn't verified yet — and the
/// <c>Auth:RequirePhoneForBooking</c> policy is on. Maps to 403 with a distinct
/// code the app catches to launch the phone-verification prompt.
/// </summary>
public sealed class PhoneVerificationRequiredException : Exception
{
    public PhoneVerificationRequiredException()
        : base("Verify your phone number to continue.") { }
}
