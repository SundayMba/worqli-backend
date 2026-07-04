namespace Servika.Domain.Payments;

/// <summary>Lifecycle of an artisan payout request.</summary>
public enum WithdrawalStatus
{
    /// <summary>Requested; funds reserved (ledger debit written), awaiting disbursement.</summary>
    Pending = 0,

    /// <summary>Successfully disbursed to the artisan's bank account.</summary>
    Paid = 1,

    /// <summary>Disbursement failed; the reserving ledger debit was reversed.</summary>
    Failed = 2,
}
