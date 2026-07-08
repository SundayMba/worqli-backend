namespace Servika.Domain.Chat;

/// <summary>
/// A one-to-one chat thread between a customer and an artisan. Identified by the
/// pair (<see cref="CustomerUserId"/>, <see cref="ArtisanId"/>) — there is at most
/// one conversation per pair, so it spans all their interactions (before, during and
/// after any booking) rather than being scoped to a single booking. The artisan's
/// login (<see cref="ArtisanUserId"/>) is captured so both sides can be authorised
/// without re-resolving the profile on every request.
/// </summary>
public sealed class Conversation
{
    public Guid Id { get; private set; }

    /// <summary>The customer (a <c>User</c>) in the conversation.</summary>
    public Guid CustomerUserId { get; private set; }

    /// <summary>The artisan's catalogue profile id.</summary>
    public Guid ArtisanId { get; private set; }

    /// <summary>The artisan's login account, if the profile is linked to one.</summary>
    public Guid? ArtisanUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Bumped on every message so the list can sort by recent activity.</summary>
    public DateTimeOffset LastMessageAtUtc { get; private set; }

    private Conversation() { }

    public static Conversation Start(
        Guid customerUserId, Guid artisanId, Guid? artisanUserId, DateTimeOffset now)
    {
        if (customerUserId == Guid.Empty)
            throw new ArgumentException("Customer is required.", nameof(customerUserId));
        if (artisanId == Guid.Empty)
            throw new ArgumentException("Artisan is required.", nameof(artisanId));

        return new Conversation
        {
            Id = Guid.NewGuid(),
            CustomerUserId = customerUserId,
            ArtisanId = artisanId,
            ArtisanUserId = artisanUserId,
            CreatedAt = now,
            LastMessageAtUtc = now,
        };
    }

    public void Touch(DateTimeOffset now) => LastMessageAtUtc = now;
}
