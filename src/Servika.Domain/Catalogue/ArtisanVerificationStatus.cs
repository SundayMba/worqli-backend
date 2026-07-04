namespace Servika.Domain.Catalogue;

/// <summary>
/// KYC/verification state of an artisan profile. A self-onboarded artisan starts
/// <see cref="Verified"/> for now (auto-approved) — real document review is the
/// admin slice's job and will move new profiles Pending → Verified/Rejected.
/// Only <see cref="Verified"/> profiles appear in the public catalogue.
/// </summary>
public enum ArtisanVerificationStatus
{
    Pending = 0,
    Verified = 1,
    Rejected = 2,
}
