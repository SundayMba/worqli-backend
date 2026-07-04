namespace Servika.Domain.Catalogue;

/// <summary>
/// Government ID an artisan can verify with. Deliberately broad — many Nigerian
/// artisans (incl. unskilled labour) may not have a NIN slip but will have a
/// voter's card or driver's licence, so any one is accepted.
/// </summary>
public enum KycIdType
{
    Nin = 0,
    VotersCard = 1,
    DriversLicense = 2,
    Passport = 3,
}
