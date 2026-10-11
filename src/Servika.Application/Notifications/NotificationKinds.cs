namespace Servika.Application.Notifications;

/// <summary>
/// Fine-grained notification kinds (the <c>kind</c> field on the feed, push data and
/// realtime event). The apps route taps on these; keep the strings stable.
/// </summary>
public static class NotificationKinds
{
    public const string KycApproved = "KycApproved";
    public const string KycChangesRequested = "KycChangesRequested";
    public const string KycDeclined = "KycDeclined";
    public const string KycResubmitted = "KycResubmitted";
    public const string OpenJob = "OpenJob";
    public const string OpenJobClaimed = "OpenJobClaimed";
    public const string NewJobRequest = "NewJobRequest";
    public const string BookingAccepted = "BookingAccepted";
    public const string BookingDeclined = "BookingDeclined";
    public const string ArtisanOnTheWay = "ArtisanOnTheWay";
    public const string ArtisanArrived = "ArtisanArrived";
    public const string WorkStarted = "WorkStarted";
    public const string BidPlaced = "BidPlaced";
    public const string BidAccepted = "BidAccepted";
    public const string CounterOffer = "CounterOffer";
    public const string CounterAccepted = "CounterAccepted";
    public const string CounterDeclined = "CounterDeclined";
    public const string MaterialsAdvance = "MaterialsAdvance";
    public const string WorkSubmitted = "WorkSubmitted";
    public const string JobConfirmed = "JobConfirmed";
    public const string BookingCancelled = "BookingCancelled";
    public const string Dispute = "Dispute";
    public const string DisputeResolved = "DisputeResolved";
    public const string Refund = "Refund";
    public const string Payout = "Payout";
    public const string FeeNotice = "FeeNotice";
    public const string Chat = "Chat";
    public const string Review = "Review";
    public const string PaymentReceived = "PaymentReceived";
    public const string PaymentSecured = "PaymentSecured";
    public const string CashChosen = "CashChosen";
    public const string PaymentMismatch = "PaymentMismatch";
    public const string BalanceSettled = "BalanceSettled";
    public const string ServiceFeeRecorded = "ServiceFeeRecorded";
}
