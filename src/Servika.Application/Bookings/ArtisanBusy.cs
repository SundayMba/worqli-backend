namespace Servika.Application.Bookings;

/// <summary>
/// An artisan is busy while one of their jobs is Arrived or In progress: they are standing in someone's home.
/// A busy artisan still sees and may bid on every open request, urgent ones included, but the offer must say when
/// they will be free. The customer sees "On a job now · free from 3:30pm" beside the other offers and decides.
/// (On the way does not count: they can still finish the trip and plan the next job.)
/// </summary>
public static class ArtisanBusy
{
    public const string NeedsTime = "You are on a job right now. Say when you will be free.";
}
