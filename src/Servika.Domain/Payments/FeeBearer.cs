namespace Servika.Domain.Payments;

/// <summary>Who absorbed a transaction fee: Servika (the launch window) or the person
/// transacting (the customer at checkout, the artisan on withdrawal).</summary>
public enum FeeBearer
{
    Platform = 0,
    User = 1,
}
