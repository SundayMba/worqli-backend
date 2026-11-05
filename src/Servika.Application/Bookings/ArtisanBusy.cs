namespace Servika.Application.Bookings;

/// <summary>
/// An artisan is busy while one of their jobs is Arrived or In progress: they are standing in someone's home.
/// Busy artisans are not offered urgent requests and cannot bid on them; on any other request their offer must
/// say when they can come. (On the way does not count: they can still finish the trip and plan the next job.)
/// </summary>
public static class ArtisanBusy
{
    public const string UrgentRefusal =
        "You are on a job right now. Urgent requests go to artisans who can leave now. Finish this one first.";

    public const string NeedsTime = "You are on a job right now. Say when you can come.";
}
