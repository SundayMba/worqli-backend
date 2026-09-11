using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>Reads the fee schedule and quotes fees for an amount, straight from the
/// admin settings. The apps never compute a fee themselves.</summary>
public sealed class FeeQuoteHandler
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public FeeQuoteHandler(IPlatformSettingsRepository settings, IClock clock)
    {
        _settings = settings;
        _clock = clock;
    }

    public async Task<FeeScheduleDto> ScheduleAsync(CancellationToken ct)
    {
        var s = await _settings.GetOrCreateAsync(ct);
        var now = _clock.UtcNow;
        var live = FeePolicy.UsersBearFees(s, now);
        int? days = s.FeesStartAtUtc is { } start && !live
            ? (int)Math.Ceiling((start - now).TotalDays)
            : null;
        return new FeeScheduleDto(
            s.FeesStartAtUtc, live, days,
            s.CardFeeRate, s.CardFeeFlatNaira, s.CardFeeFlatFromNaira, s.CardFeeCapNaira,
            new[]
            {
                new TransferFeeTierDto(s.TransferFeeTier1MaxNaira, s.TransferFeeTier1Naira),
                new TransferFeeTierDto(s.TransferFeeTier2MaxNaira, s.TransferFeeTier2Naira),
                new TransferFeeTierDto(null, s.TransferFeeTier3Naira),
            });
    }

    public async Task<FeeQuoteDto> QuoteAsync(int amountNaira, CancellationToken ct)
    {
        if (amountNaira < 0 || amountNaira > 100_000_000)
            throw new ArgumentException("Amount must be between 0 and 100,000,000.", nameof(amountNaira));
        var s = await _settings.GetOrCreateAsync(ct);
        var live = FeePolicy.UsersBearFees(s, _clock.UtcNow);
        var service = live ? FeePolicy.CustomerServiceFee(amountNaira, s) : 0;
        var transfer = live ? FeePolicy.TransferFee(amountNaira, s) : 0;
        return new FeeQuoteDto(
            amountNaira, service, amountNaira + service,
            transfer, Math.Max(0, amountNaira - transfer),
            live, s.FeesStartAtUtc);
    }
}
