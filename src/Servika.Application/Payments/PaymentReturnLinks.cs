namespace Servika.Application.Payments;

/// <summary>
/// Deep links the hosted checkout redirects to when a charge completes, so the payer
/// lands back inside the app that started the payment. Each app opens the checkout in
/// an auth session that closes itself the moment its scheme is hit, then polls for the
/// webhook-settled state. Schemes match the apps' <c>app.json</c> (<c>servika</c> =
/// customer app, <c>servikapro</c> = Servika Pro).
/// </summary>
public static class PaymentReturnLinks
{
    public static string CustomerBooking(Guid bookingId) =>
        $"servika://payment/callback?bookingId={bookingId}";

    public const string ArtisanSettlement = "servikapro://payment/callback?settlement=1";
}
