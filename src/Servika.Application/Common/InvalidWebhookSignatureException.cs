namespace Servika.Application.Common;

/// <summary>
/// Thrown when a payment webhook's signature can't be verified — the body may be
/// forged. The API maps this to 401 Unauthorized and the event is ignored.
/// </summary>
public sealed class InvalidWebhookSignatureException : Exception
{
    public InvalidWebhookSignatureException()
        : base("The webhook signature is missing or invalid.")
    {
    }
}
