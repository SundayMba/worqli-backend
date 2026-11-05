using Servika.Application.Notifications;
using Servika.Domain.Notifications;

namespace Servika.UnitTests.Notifications;

public class NotificationAudienceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();

    private static PushToken Token(string token, string? app) => PushToken.Register(User, token, "android", Now, app);

    private static readonly PushToken[] Tokens =
    {
        Token("customer-phone", "customer"),
        Token("pro-phone", "pro"),
        Token("old-build", null),
    };

    [Fact]
    public void Any_reaches_every_token()
    {
        Assert.Equal(new[] { "customer-phone", "pro-phone", "old-build" },
            NotificationAudienceFilter.TokensFor(Tokens, NotificationAudience.Any));
    }

    [Fact]
    public void Customer_notice_skips_the_pro_app()
    {
        Assert.Equal(new[] { "customer-phone", "old-build" },
            NotificationAudienceFilter.TokensFor(Tokens, NotificationAudience.Customer));
    }

    [Fact]
    public void Artisan_notice_skips_the_customer_app()
    {
        Assert.Equal(new[] { "pro-phone", "old-build" },
            NotificationAudienceFilter.TokensFor(Tokens, NotificationAudience.Artisan));
    }

    [Fact]
    public void Older_builds_without_an_app_get_everything()
    {
        foreach (var audience in Enum.GetValues<NotificationAudience>())
            Assert.True(NotificationAudienceFilter.Reaches(audience, null));
    }

    [Fact]
    public void Notification_defaults_to_any_audience()
    {
        var n = Notification.Create(User, NotificationType.System, "Hi", "Body", null, null, Now);
        Assert.Equal(NotificationAudience.Any, n.Audience);

        var pro = Notification.Create(User, NotificationType.Booking, "Hi", "Body", null, null, Now, "OpenJob", NotificationAudience.Artisan);
        Assert.Equal(NotificationAudience.Artisan, pro.Audience);
    }

    [Theory]
    [InlineData("customer", "customer")]
    [InlineData(" PRO ", "pro")]
    [InlineData("Customer", "customer")]
    [InlineData("admin", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Push_token_app_is_normalised(string? given, string? expected)
    {
        Assert.Equal(expected, Token("t", given).App);
    }

    [Fact]
    public void Reassigning_a_token_updates_its_app()
    {
        var t = Token("t", "customer");
        t.Reassign(User, "ios", Now, "PRO");
        Assert.Equal("pro", t.App);
        t.Reassign(User, "ios", Now);
        Assert.Null(t.App);
    }

    [Theory]
    [InlineData("customer", NotificationAudience.Customer)]
    [InlineData("ARTISAN", NotificationAudience.Artisan)]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    public void Feed_query_parses(string? query, NotificationAudience? expected)
    {
        Assert.Equal(expected, NotificationAudienceFilter.ParseQuery(query));
    }

    [Fact]
    public void Feed_query_rejects_unknown_values()
    {
        Assert.Throws<ArgumentException>(() => NotificationAudienceFilter.ParseQuery("pro"));
    }
}
