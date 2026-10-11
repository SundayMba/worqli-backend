using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Payments;
using Servika.Domain.Notifications;

namespace Servika.Application.Notifications;

/// <summary>
/// Central place that turns domain events (a booking transition, a settled
/// payment) into in-app notifications for the affected customer. It only
/// <c>Add</c>s to the shared DbContext — the calling handler's
/// <c>SaveChangesAsync</c> flushes the notification in the same transaction as the
/// change that caused it, so the two can never drift apart.
///
/// <para>Copy lives here (not in the entity or handlers) so wording stays
/// consistent and easy to tune. Recipients are all the booking's customer, so no
/// artisan-profile → user lookup is needed.</para>
/// </summary>
public sealed class NotificationEmitter
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationPushDispatcher _push;
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly Payments.ArtisanStandingService _standing;
    private readonly IClock _clock;
    private readonly IAdminAlerts _alerts;
    private readonly IPlatformSettingsRepository _settings;

    public NotificationEmitter(
        IPlatformSettingsRepository settings,
        INotificationRepository notifications,
        INotificationPushDispatcher push,
        ICatalogueRepository catalogue,
        IUserRepository users,
        Payments.ArtisanStandingService standing,
        IClock clock,
        IAdminAlerts alerts)
    {
        _alerts = alerts;
        _settings = settings;
        _notifications = notifications;
        _push = push;
        _catalogue = catalogue;
        _users = users;
        _standing = standing;
        _clock = clock;
    }

    /// <summary>Notify the customer of an artisan-driven booking transition.
    /// <paramref name="refundedNaira"/> is what a cancellation actually returned.</summary>
    public async Task BookingAdvancedByArtisanAsync(
        Booking booking, ArtisanBookingAction action, int refundedNaira, CancellationToken ct)
    {
        var who = string.IsNullOrWhiteSpace(booking.ArtisanName) ? "Your artisan" : booking.ArtisanName!;
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "your" : booking.ServiceName;

        string payLine = string.Empty;
        if (action == ArtisanBookingAction.Accept && booking.InitialQuoteAmountNaira is { } due &&
            booking.PaymentState != BookingPaymentState.Paid && booking.PaymentMethod != PaymentMethod.Cash)
        {
            // Quote the total the card will actually be charged once users bear fees.
            var s = await _settings.GetOrCreateAsync(ct);
            var fee = Domain.Payments.FeePolicy.UsersBearFees(s, _clock.UtcNow)
                ? Domain.Payments.FeePolicy.CustomerServiceFee(due, s)
                : 0;
            payLine = fee > 0
                ? $" Pay ₦{due + fee:N0} (₦{due:N0} plus a ₦{fee:N0} payment fee) to secure it. Your money is held safely until the job is done."
                : $" Pay ₦{due:N0} to secure it. Your money is held safely until the job is done.";
        }

        var (kind, title, body) = action switch
        {
            // A fixed-price booking already has its amount — nudge the payment
            // that unlocks the work; a quote-based accept has nothing due yet.
            ArtisanBookingAction.Accept =>
                (NotificationKinds.BookingAccepted, "Booking accepted", $"{who} accepted your {service} booking.{payLine}"),
            ArtisanBookingAction.Reject =>
                (NotificationKinds.BookingDeclined, "Booking declined", $"{who} can't take this {service} booking. Try another artisan."),
            ArtisanBookingAction.StartTrip =>
                (NotificationKinds.ArtisanOnTheWay, "Artisan on the way", $"{who} is heading to your location."),
            ArtisanBookingAction.Arrive =>
                (NotificationKinds.ArtisanArrived, "Artisan arrived", $"{who} has arrived at your location."),
            ArtisanBookingAction.StartWork =>
                (NotificationKinds.WorkStarted, "Work started", $"{who} has started your {service} job."),
            ArtisanBookingAction.Cancel =>
                (NotificationKinds.BookingCancelled, "Booking cancelled",
                 $"{who} can no longer take your {service} booking. {CancelRefundLine(booking, refundedNaira)} You can book another artisan right away."),
            _ => (string.Empty, string.Empty, string.Empty),
        };

        if (title.Length == 0) return; // nothing to announce for this action
        Add(booking.CustomerId, NotificationType.Booking, kind, title, body, booking.Id);
    }

    /// <summary>The honest refund sentence for an artisan cancellation.</summary>
    private static string CancelRefundLine(Booking booking, int refundedNaira)
    {
        var advance = booking.ReleasedMaterialsAdvanceNaira;
        if (refundedNaira > 0 && advance > 0)
            return $"₦{refundedNaira:N0} of your payment is being refunded. The ₦{advance:N0} materials advance you approved stays with the artisan; contact support if you need help with it.";
        if (refundedNaira > 0)
            return $"Your ₦{refundedNaira:N0} payment is being refunded in full.";
        return "Nothing was charged through Servika.";
    }

    /// <summary>Tell the customer their artisan submitted completed work to review.</summary>
    public void WorkSubmitted(Booking booking)
    {
        var who = string.IsNullOrWhiteSpace(booking.ArtisanName) ? "Your artisan" : booking.ArtisanName!;
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.WorkSubmitted,
            "Work completed", $"{who} finished your {service}. Review the photos and confirm.", booking.Id);
    }

    /// <summary>Tell the customer a job auto-confirmed because they didn't respond in time.</summary>
    public void BookingAutoConfirmed(Booking booking)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.JobConfirmed,
            "Job auto-confirmed", $"Your {service} was auto-confirmed as complete. Tap to leave a review.", booking.Id);
    }

    /// <summary>Nudge the customer to review a job they just confirmed complete.</summary>
    public void BookingCompleted(Booking booking)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.JobConfirmed,
            "Job completed", $"Your {service} is done. Tap to leave a review.", booking.Id);
    }

    /// <summary>Tell the customer AND the assigned artisan how a dispute was resolved.
    /// <paramref name="refundedNaira"/> is what was actually refunded (0 for cash or
    /// unpaid jobs), so neither side is ever told about a refund that did not happen.</summary>
    public async Task DisputeResolvedAsync(
        Booking booking, bool favourCustomer, bool partial, int refundedNaira, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "booking" : $"{booking.ServiceName} booking";
        var advance = booking.ReleasedMaterialsAdvanceNaira;

        string customerBody, artisanBody;
        if (!favourCustomer)
        {
            customerBody = $"Your dispute on the {service} was reviewed and the job stands.";
            artisanBody = booking.PaymentState == BookingPaymentState.Paid
                ? $"The dispute on the {service} was resolved in your favour. The job is complete and your earnings are released to your balance."
                : $"The dispute on the {service} was resolved in your favour. The job is complete.";
        }
        else if (partial)
        {
            customerBody = refundedNaira > 0
                ? $"Your dispute on the {service} was resolved with a partial refund: ₦{refundedNaira:N0} is coming back to you, and the artisan keeps the rest for the work done."
                : $"Your dispute on the {service} was resolved and the job is closed as done. Nothing was paid through Servika, so there is no refund to send.";
            artisanBody = refundedNaira > 0
                ? $"The dispute on the {service} was resolved with a partial refund of ₦{refundedNaira:N0} to the customer. You keep the rest of the payment."
                : $"The dispute on the {service} was resolved and the job is closed as done.";
        }
        else
        {
            customerBody = refundedNaira > 0
                ? $"Your dispute on the {service} was resolved in your favour. ₦{refundedNaira:N0} is being refunded to you."
                : $"Your dispute on the {service} was resolved in your favour and the booking is cancelled.";
            artisanBody = $"The dispute on the {service} was resolved in the customer's favour and the booking is cancelled." +
                          (refundedNaira > 0 ? " Their payment is refunded." : string.Empty) +
                          (advance > 0 ? $" The ₦{advance:N0} materials advance you received stays with you." : string.Empty);
        }

        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.DisputeResolved, "Dispute resolved", customerBody, booking.Id);
        await NotifyArtisanAsync(booking, NotificationKinds.DisputeResolved, "Dispute resolved", artisanBody, ct);
    }

    /// <summary>Tell the customer a refund is on its way (requested, settling async).</summary>
    public void RefundIssued(Booking booking, int amountNaira)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "booking" : $"{booking.ServiceName} booking";
        Add(booking.CustomerId, NotificationType.Payment, NotificationKinds.Refund,
            "Refund on its way",
            $"We're sending ₦{amountNaira:N0} for your {service} back to your account. It can take a few days to appear.",
            booking.Id);
    }

    /// <summary>Confirm to the customer that their refund actually landed.</summary>
    public void RefundSettled(Guid customerId, Guid bookingId, string serviceName, int amountNaira)
    {
        var service = string.IsNullOrWhiteSpace(serviceName) ? "booking" : $"{serviceName} booking";
        Add(customerId, NotificationType.Payment, NotificationKinds.Refund,
            "Refund complete", $"₦{amountNaira:N0} for your {service} has landed in your account.", bookingId);
    }

    /// <summary>Alert every admin that a refund failed at the gateway and needs a
    /// manual retry. The customer's ledger refund stands (they won the dispute); only
    /// the money movement failed, so this is an ops action, not a customer message.</summary>
    public async Task RefundFailedNeedsRetryAsync(Guid bookingId, int amountNaira, CancellationToken ct)
    {
        var admins = await _users.ListAsync(Domain.Users.Role.Admin, ct);
        var superAdmins = await _users.ListAsync(Domain.Users.Role.SuperAdmin, ct);
        var body = $"A ₦{amountNaira:N0} refund for booking {bookingId.ToString()[..8].ToUpperInvariant()} " +
                   "failed at Paystack. Reprocess the refund from the Paystack dashboard.";
        foreach (var admin in admins.Concat(superAdmins))
            Add(admin.Id, NotificationType.System, NotificationKinds.Refund, "Refund failed", body, bookingId);
        _alerts.Send(new AdminAlert(AdminAlertKind.RefundFailed, "Refund failed at Paystack", new[] { body }, $"/bookings/{bookingId}", "Open booking"));
    }

    /// <summary>A webhook reported a successful charge for LESS than we asked (or in
    /// another currency). The payment was failed, nothing was settled; every admin is
    /// told because this is either a gateway fault or an attack.</summary>
    public async Task PaymentAmountMismatchAsync(
        Guid? bookingId, string reference, int expectedNaira, int? paidNaira, string? currency, CancellationToken ct)
    {
        var admins = await _users.ListAsync(Domain.Users.Role.Admin, ct);
        var superAdmins = await _users.ListAsync(Domain.Users.Role.SuperAdmin, ct);
        var what = paidNaira is { } p ? $"₦{p:N0}" : "an unknown amount";
        var cur = currency is { } c && !string.Equals(c, "NGN", StringComparison.OrdinalIgnoreCase) ? $" in {c}" : "";
        var body = $"Payment {reference} reported {what}{cur} against ₦{expectedNaira:N0} due. " +
                   "It was NOT settled and the booking stays unpaid. Check the transaction in Paystack.";
        foreach (var admin in admins.Concat(superAdmins))
            Add(admin.Id, NotificationType.System, NotificationKinds.PaymentMismatch, "Payment amount mismatch", body, bookingId);
        _alerts.Send(new AdminAlert(AdminAlertKind.PaymentMismatch, "Payment amount mismatch", new[] { body },
            bookingId is { } bid ? $"/bookings/{bid}" : "/payments", bookingId is null ? "Open payments" : "Open booking"));
    }

    /// <summary>Advance notice that transaction fees are about to start (or just did).
    /// Customers hear about the payment fee; artisans about the transfer charge.</summary>
    public void FeeNotice(Guid userId, bool isArtisan, int stage, DateTimeOffset startAtUtc, Domain.Settings.PlatformSettings s)
    {
        var date = startAtUtc.ToOffset(TimeSpan.FromHours(1)).ToString("d MMMM yyyy"); // Lagos time
        var pct = (s.CardFeeRate * 100).ToString("0.##");
        var when = stage switch
        {
            Domain.Settings.PlatformSettings.FeeNoticeLive => "from today",
            Domain.Settings.PlatformSettings.FeeNotice1Day => "from tomorrow",
            _ => $"from {date}",
        };
        string title, body;
        if (isArtisan)
        {
            title = stage == Domain.Settings.PlatformSettings.FeeNoticeLive ? "Transfer charges now apply" : $"Transfer charges start {date}";
            body = $"Servika has been covering the bank transfer charge on your withdrawals. {Cap(when)}, the charge comes off each withdrawal: " +
                   $"₦{s.TransferFeeTier1Naira:N0} up to ₦{s.TransferFeeTier1MaxNaira:N0}, ₦{s.TransferFeeTier2Naira:N0} up to ₦{s.TransferFeeTier2MaxNaira:N0}, ₦{s.TransferFeeTier3Naira:N0} above that. " +
                   "Withdraw less often to pay fewer charges. Your earnings themselves are not affected.";
        }
        else
        {
            title = stage == Domain.Settings.PlatformSettings.FeeNoticeLive ? "Payment fees now apply" : $"Payment fees start {date}";
            body = $"Servika has been covering the card and transfer fee on online payments. {Cap(when)}, a payment fee of {pct}% " +
                   (s.CardFeeFlatNaira > 0 ? $"(plus ₦{s.CardFeeFlatNaira:N0} on payments of ₦{s.CardFeeFlatFromNaira:N0} or more" + (s.CardFeeCapNaira > 0 ? $", never more than ₦{s.CardFeeCapNaira:N0}" : "") + ") " : "") +
                   "is shown and added when you pay online. The agreed price itself does not change, and cash jobs are unaffected.";
        }
        Add(userId, NotificationType.System, NotificationKinds.FeeNotice, title, body, null);

        static string Cap(string t) => char.ToUpperInvariant(t[0]) + t[1..];
    }

    /// <summary>Confirm to the customer that their payment settled.</summary>
    public void PaymentReceived(Guid customerId, Guid bookingId, string serviceName)
    {
        var service = string.IsNullOrWhiteSpace(serviceName) ? "your booking" : serviceName;
        Add(customerId, NotificationType.Payment, NotificationKinds.PaymentReceived,
            "Payment received", $"We received your payment for {service}.", bookingId);
    }

    // ── Artisan-facing notifications ────────────────────────────────────────
    // Recipient is the assigned artisan's login account, resolved from the
    // booking's ArtisanId (a catalogue profile id) → its linked UserId. A no-op if
    // the booking has no artisan or the profile isn't linked to a user account.

    /// <summary>Tell the assigned artisan a new job was booked with them.</summary>
    public Task ArtisanNewBooking(Booking booking, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : booking.ServiceName;
        return NotifyArtisanAsync(booking, NotificationKinds.NewJobRequest,
            "New job request", $"You have a new {service} request. Review and accept it.", ct);
    }

    /// <summary>Tell the assigned artisan the customer cancelled the booking.</summary>
    public Task ArtisanBookingCancelled(Booking booking, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        return NotifyArtisanAsync(booking, NotificationKinds.BookingCancelled,
            "Booking cancelled", $"The customer cancelled the {service}.", ct);
    }

    /// <summary>Tell the artisan the customer confirmed the completed work.</summary>
    /// <param name="cashServiceFeeNaira">Service fee recorded on a cash job (0 if none).</param>
    public Task ArtisanJobConfirmed(Booking booking, int cashServiceFeeNaira, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        string tail;
        if (booking.PaymentState == BookingPaymentState.Paid)
            tail = " Your earnings are released to your balance.";
        else if (booking.PaymentMethod == PaymentMethod.Cash)
            tail = " The customer paid you in cash." +
                   (cashServiceFeeNaira > 0 ? $" A ₦{cashServiceFeeNaira:N0} Servika service fee was recorded on your balance." : string.Empty);
        else
            tail = string.Empty;
        return NotifyArtisanAsync(booking, NotificationKinds.JobConfirmed,
            "Job confirmed", $"Your {service} is confirmed complete.{tail}", ct);
    }

    /// <summary>Tell the artisan a customer left them a review.</summary>
    public Task ArtisanNewReview(Booking booking, int rating, CancellationToken ct)
    {
        return NotifyArtisanAsync(booking, NotificationKinds.Review,
            "New review", $"A customer rated your work {rating}★. Tap to view.", ct);
    }

    /// <summary>Broadcast a newly-posted open request to every verified artisan whose
    /// services include its category and whose work rules accept it (not away, within
    /// their work radius, emergencies only if they take them). Carries the booking id
    /// so the Pro app can open /open-job/{id}.</summary>
    public async Task OpenJobPosted(Booking booking, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : booking.ServiceName;
        var recipients = await _catalogue.ListArtisanRecipientsInCategoryAsync(booking.CategorySlug, ct);
        // Artisans past the commission-debt limit don't receive new requests
        // until they settle — the enforcement half of commission-on-cash.
        var restricted = (await _standing.GetRestrictedProfileIdsAsync(ct)).ToHashSet();
        var bidding = booking.Assessment == Domain.Bookings.AssessmentMode.RemoteQuote;
        var now = _clock.UtcNow;
        foreach (var recipient in recipients.Where(r => !restricted.Contains(r.ProfileId)
                     && OpenJobMatching.Accepts(r.Rules, booking, now)))
        {
            Add(recipient.UserId, NotificationType.OpenJob, NotificationKinds.OpenJob,
                bidding ? "New job: send your price" : "New job available",
                bidding
                    ? $"A new {service} request is open for bids. Check the photos and offer your price."
                    : $"A new {service} request is open near you. Tap to view and accept.",
                booking.Id);
        }
    }

    /// <summary>Tell the customer an artisan offered a price on their request.
    /// A broadcast invites comparison; a direct request has one quote to review.</summary>
    public void BidPlaced(Booking booking, Bid bid)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "request" : $"{booking.ServiceName} request";
        var direct = booking.ArtisanId is not null;
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.BidPlaced,
            direct ? "Quote received" : "New price offer",
            direct
                ? $"{bid.ArtisanName} sent a quote of ₦{bid.AmountNaira:N0} for your {service}. Review and accept to proceed."
                : $"{bid.ArtisanName} offered ₦{bid.AmountNaira:N0} for your {service}. Compare offers and pick your artisan.",
            booking.Id);
    }

    /// <summary>Tell the artisan the customer countered their workmanship price.</summary>
    public void CounterOfferMade(Booking booking, Bid bid)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        var total = (bid.PendingCounterNaira ?? 0) + bid.MaterialsNaira;
        var note = string.IsNullOrWhiteSpace(bid.PendingCounterNote) ? string.Empty : $" \"{bid.PendingCounterNote}\"";
        Add(bid.ArtisanUserId, NotificationType.Booking, NotificationKinds.CounterOffer,
            "Customer made an offer",
            $"The customer offered ₦{bid.PendingCounterNaira:N0} for workmanship on the {service} " +
            $"(₦{total:N0} with materials).{note} Accept it, decline, or send a new price.",
            booking.Id);
    }

    /// <summary>Tell the customer the artisan turned down their counter; the quote stands.</summary>
    public void CounterOfferDeclined(Booking booking, Bid bid, int declinedNaira)
    {
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.CounterDeclined,
            "Offer declined",
            $"{bid.ArtisanName} didn't accept ₦{declinedNaira:N0} for workmanship. " +
            $"Their quote of ₦{bid.AmountNaira:N0} still stands; you can accept it or make another offer.",
            booking.Id);
    }

    /// <summary>Tell the customer the artisan took their offer — the price is agreed.</summary>
    public void CounterOfferAccepted(Booking booking, Bid bid, BookingStatus statusBefore)
    {
        var next = statusBefore is BookingStatus.Open or BookingStatus.Pending
            ? "Pay securely in the app to lock it in."
            : "Pay securely in the app so the work can start.";
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.CounterAccepted,
            "Your offer was accepted",
            $"{bid.ArtisanName} accepted your offer. The agreed price is ₦{bid.AmountNaira:N0}. {next}",
            booking.Id);
    }

    /// <summary>Tell the customer the artisan answered their counter with a new price.</summary>
    public void BidRevisedAfterCounter(Booking booking, Bid bid)
    {
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.BidPlaced,
            "New price from " + bid.ArtisanName,
            $"{bid.ArtisanName} came back with ₦{bid.AmountNaira:N0} (₦{bid.WorkmanshipNaira:N0} workmanship). " +
            "Accept it, or make another offer.",
            booking.Id);
    }

    /// <summary>Tell the winning artisan the customer accepted their price.
    /// Copy depends on where the job stood when they accepted (acceptance itself
    /// mutates the booking, so the caller passes the pre-acceptance status): a
    /// broadcast win means "head out"; a pre-visit quote waits on payment; an
    /// en-route/on-site quote can start the moment the payment gate clears.</summary>
    public void BidAccepted(Booking booking, Bid bid, BookingStatus statusBeforeAccept)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        var secured = booking.PaymentState == BookingPaymentState.Paid || booking.PaymentMethod == PaymentMethod.Cash;
        var body = statusBeforeAccept switch
        {
            BookingStatus.Open when secured =>
                $"You won the {service} at ₦{bid.AmountNaira:N0}. Head out when ready!",
            BookingStatus.Open =>
                $"You won the {service} at ₦{bid.AmountNaira:N0}. You will be told once payment is secured.",
            BookingStatus.Pending =>
                $"Your ₦{bid.AmountNaira:N0} quote for the {service} was accepted. You'll be notified once payment is secured.",
            _ =>
                $"The customer accepted your ₦{bid.AmountNaira:N0} quote. You can start as soon as payment is secured.",
        };
        Add(bid.ArtisanUserId, NotificationType.Booking, NotificationKinds.BidAccepted, "Your offer was accepted", body, booking.Id);
    }

    /// <summary>Tell the artisan the escrow is funded — the start-work gate is open.</summary>
    public Task ArtisanEscrowFunded(Booking booking, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        var amount = booking.InitialQuoteAmountNaira is { } a ? $"₦{a:N0} " : string.Empty;
        return NotifyArtisanAsync(booking, NotificationKinds.PaymentSecured,
            "Payment secured",
            $"{amount}for the {service} is held in escrow. You're clear to start the work.", ct);
    }

    /// <summary>Ask the customer to release part of the materials money early.</summary>
    public void MaterialsAdvanceRequested(Booking booking, int amountNaira)
    {
        var who = string.IsNullOrWhiteSpace(booking.ArtisanName) ? "Your artisan" : booking.ArtisanName;
        Add(booking.CustomerId, NotificationType.Payment, NotificationKinds.MaterialsAdvance,
            "Materials money requested",
            $"{who} asked for ₦{amountNaira:N0} of the agreed materials cost to buy parts now. " +
            "Approve in the app to release it; your workmanship payment stays held until the job is done.",
            booking.Id);
    }

    /// <summary>Tell the artisan how the customer answered the materials request.</summary>
    public Task MaterialsAdvanceDecided(Booking booking, bool approved, int amountNaira, CancellationToken ct) =>
        approved
            ? NotifyArtisanAsync(booking, NotificationKinds.MaterialsAdvance,
                "Materials money released",
                $"₦{amountNaira:N0} is now in your Servika wallet to buy the materials. " +
                "The rest of the price is released when the customer confirms the job.", ct)
            : NotifyArtisanAsync(booking, NotificationKinds.MaterialsAdvance,
                "Materials request declined",
                "The customer chose not to release materials money up front. " +
                "You can ask again with a smaller amount, or discuss it with them in chat.", ct);

    /// <summary>Tell the artisan the customer chose to pay cash after service.</summary>
    public Task ArtisanCashChosen(Booking booking, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        var amount = booking.InitialQuoteAmountNaira is { } a ? $"₦{a:N0} " : string.Empty;
        return NotifyArtisanAsync(booking, NotificationKinds.CashChosen,
            "Cash payment chosen",
            $"The customer will pay {amount}in cash after the {service}. You're clear to start the work.", ct);
    }

    /// <summary>Tell the artisan a cash job's service fee was recorded against
    /// their balance — transparency, never a surprise deduction.</summary>
    public Task ArtisanCommissionRecorded(Booking booking, int commissionNaira, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "job" : $"{booking.ServiceName} job";
        return NotifyArtisanAsync(booking, NotificationKinds.ServiceFeeRecorded,
            "Service fee recorded",
            $"A ₦{commissionNaira:N0} Servika fee applies to your cash {service}. It'll be deducted from your next online earnings, or you can settle it anytime from Earnings.", ct);
    }

    /// <summary>Tell the payout requester their bank transfer landed.</summary>
    public void PayoutSent(Guid userId, int amountNaira, string bankMasked)
    {
        Add(userId, NotificationType.Payment, NotificationKinds.Payout,
            "Payout sent",
            $"₦{amountNaira:N0} was sent to your bank account {bankMasked}.",
            null);
    }

    /// <summary>The reviewer decided on the artisan's verification. A decline carries the
    /// reviewer's note word for word, so the artisan knows exactly what to fix.</summary>
    public void KycReviewed(Guid artisanUserId, bool approved, string? note, string? check = null)
    {
        if (approved)
        {
            Add(artisanUserId, NotificationType.System, NotificationKinds.KycApproved,
                "You are verified",
                "Your checks passed. Your profile now carries the verified badge and you can take jobs.",
                null);
            return;
        }
        var reason = string.IsNullOrWhiteSpace(note) ? "The reviewer could not match your documents." : note.Trim();
        var what = CheckLabel(check);
        Add(artisanUserId, NotificationType.System, NotificationKinds.KycDeclined,
            what is null ? "Your verification needs another look" : $"Your verification needs another look: {what}",
            $"{reason} Fix it in Get verified and send again; it goes to the front of the queue.",
            null);
    }

    /// <summary>The reviewer asked for one check to be fixed; the note is quoted word for word.</summary>
    public void KycChangesRequested(Guid artisanUserId, string check, string note)
    {
        var what = CheckLabel(check) ?? "one check";
        // An ID or selfie fix is a fresh upload, which sends itself back; every other
        // check is edited and then sent back by hand.
        var how = check switch
        {
            "Identity" => "Upload your ID again in Get verified; it goes straight back to the reviewer and you go to the front of the queue.",
            "Selfie" => "Take your selfie again in Get verified; it goes straight back to the reviewer and you go to the front of the queue.",
            _ => "Fix it in Get verified and tap Send it back for review; you go to the front of the queue.",
        };
        Add(artisanUserId, NotificationType.System, NotificationKinds.KycChangesRequested,
            $"One change needed: {what}",
            $"{note} {how}",
            null);
    }

    /// <summary>Plain-language name of a verification check, or null if unknown.</summary>
    private static string? CheckLabel(string? check) => check switch
    {
        "Trade" => "your trade and work area",
        "Photo" => "your profile photo",
        "Identity" => "your ID",
        "Selfie" => "your selfie",
        "Guarantors" => "your guarantors",
        "Payout" => "your payout account",
        "Documents" => "your documents",
        _ => null,
    };

    /// <summary>Tell the admins an artisan sent a fix back, so the desk sees it without refreshing.</summary>
    public async Task KycResubmittedAsync(string artisanName, string check, CancellationToken ct)
    {
        var admins = await _users.ListAsync(Domain.Users.Role.Admin, ct);
        var superAdmins = await _users.ListAsync(Domain.Users.Role.SuperAdmin, ct);
        foreach (var admin in admins.Concat(superAdmins))
            Add(admin.Id, NotificationType.System, NotificationKinds.KycResubmitted, "Resubmission to review", $"{artisanName} fixed {check.ToLowerInvariant()} and sent their application back.", null);
    }

    /// <summary>Tell the payout requester the transfer failed and funds were returned.</summary>
    public void PayoutFailed(Guid userId, int amountNaira)
    {
        Add(userId, NotificationType.Payment, NotificationKinds.Payout,
            "Payout failed",
            $"Your ₦{amountNaira:N0} payout couldn't be completed. The funds are back in your balance. Please check your bank details and try again.",
            null);
    }

    /// <summary>Tell the artisan their settlement landed and they're active again.</summary>
    public void ArtisanBalanceSettled(Guid artisanUserId, int amountNaira)
    {
        Add(artisanUserId, NotificationType.Payment, NotificationKinds.BalanceSettled,
            "Balance settled",
            $"₦{amountNaira:N0} received. Your service fees are cleared and you're receiving job requests again.",
            null);
    }

    /// <summary>Tell the customer an artisan claimed their open request.</summary>
    public void OpenJobClaimed(Booking booking)
    {
        var who = string.IsNullOrWhiteSpace(booking.ArtisanName) ? "An artisan" : booking.ArtisanName!;
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "request" : $"{booking.ServiceName} request";
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.OpenJobClaimed,
            "Artisan found", $"{who} accepted your {service} and will be in touch.", booking.Id);
    }

    private async Task NotifyArtisanAsync(Booking booking, string kind, string title, string body, CancellationToken ct)
    {
        if (booking.ArtisanId is not { } profileId) return;
        var profile = await _catalogue.GetArtisanByIdAsync(profileId, ct);
        if (profile?.UserId is not { } artisanUserId) return; // unlinked catalogue artisan
        Add(artisanUserId, NotificationType.Booking, kind, title, body, booking.Id);
    }

    /// <summary>Tell the assigned artisan a customer opened a dispute, so they can
    /// give their side in the app.</summary>
    public Task DisputeRaisedForArtisanAsync(Booking booking, CancellationToken ct)
    {
        var service = string.IsNullOrWhiteSpace(booking.ServiceName) ? "a booking" : $"the {booking.ServiceName} booking";
        return NotifyArtisanAsync(booking, NotificationKinds.Dispute,
            "A customer reported an issue",
            $"A customer opened a dispute on {service}. Open it to give your side before it's reviewed.",
            ct);
    }

    /// <summary>Tell the customer + all admins that the artisan responded to a dispute.</summary>
    public async Task ArtisanRespondedToDisputeAsync(Booking booking, CancellationToken ct)
    {
        var who = string.IsNullOrWhiteSpace(booking.ArtisanName) ? "The artisan" : booking.ArtisanName!;
        Add(booking.CustomerId, NotificationType.Booking, NotificationKinds.Dispute,
            "Artisan responded", $"{who} responded to your reported issue.", booking.Id);

        var admins = await _users.ListAsync(Domain.Users.Role.Admin, ct);
        var superAdmins = await _users.ListAsync(Domain.Users.Role.SuperAdmin, ct);
        foreach (var admin in admins.Concat(superAdmins))
            Add(admin.Id, NotificationType.System, NotificationKinds.Dispute,
                "Dispute updated", $"{who} added a response to a dispute. Review it before resolving.", booking.Id);
        _alerts.Send(new AdminAlert(AdminAlertKind.DisputeResponse, "Artisan answered a dispute",
            new[] { $"{who} gave their side on the {booking.ServiceName} booking. You can resolve it now." },
            "/disputes", "Open disputes"));
    }

    /// <summary>Notify the recipient of a new chat message from the other party.
    /// The feed is <b>coalesced</b> — only one unread chat notification per
    /// conversation, so a burst of messages doesn't flood the bell — but a push fires
    /// for every message (like any chat app). <paramref name="preview"/> is the
    /// already-redacted message body.</summary>
    public async Task ChatMessageReceivedAsync(
        Guid recipientUserId, string senderName, string preview, Guid conversationId, CancellationToken ct)
    {
        var title = string.IsNullOrWhiteSpace(senderName) ? "New message" : senderName.Trim();
        var body = string.IsNullOrWhiteSpace(preview) ? "Sent you a message." : preview.Trim();
        if (body.Length > 140) body = body[..140].TrimEnd() + "…";

        var alreadyPending = await _notifications.HasUnreadChatAsync(recipientUserId, conversationId, ct);
        if (!alreadyPending)
        {
            _notifications.Add(Notification.Create(
                recipientUserId, NotificationType.Chat, title, body, null, conversationId, _clock.UtcNow,
                NotificationKinds.Chat));
        }
        // Push every message regardless of feed coalescing (best-effort, off-transaction).
        _push.Dispatch(recipientUserId, title, body, null, conversationId,
            NotificationKinds.Chat, NotificationType.Chat.ToString());
    }

    private void Add(
        Guid userId, NotificationType type, string kind, string title, string body,
        Guid? bookingId, Guid? conversationId = null)
    {
        _notifications.Add(Notification.Create(userId, type, title, body, bookingId, conversationId, _clock.UtcNow, kind));
        // Best-effort device push (fire-and-forget; independent of this transaction).
        _push.Dispatch(userId, title, body, bookingId, conversationId, kind, type.ToString());
    }
}
