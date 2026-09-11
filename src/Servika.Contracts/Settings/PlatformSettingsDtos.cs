namespace Servika.Contracts.Settings;

/// <summary>The marketplace's admin-controlled business settings.</summary>
public sealed record PlatformSettingsDto(
    decimal CommissionRate,
    decimal EmergencyCommissionRate,
    int AutoConfirmHours,
    int MinWithdrawalNaira,
    int ReferralRewardNaira,
    int MaxCommissionDebtNaira,
    DateTimeOffset UpdatedAtUtc,
    bool RequireGuarantors = false,
    int RequiredGuarantorCount = 2,
    DateTimeOffset? FeesStartAtUtc = null,
    decimal CardFeeRate = 0.015m,
    int CardFeeFlatNaira = 100,
    int CardFeeFlatFromNaira = 2500,
    int CardFeeCapNaira = 2000,
    int TransferFeeTier1Naira = 10,
    int TransferFeeTier1MaxNaira = 5000,
    int TransferFeeTier2Naira = 25,
    int TransferFeeTier2MaxNaira = 50000,
    int TransferFeeTier3Naira = 50,
    /// <summary>0 none, 1 = 30-day notice sent, 2 = 7-day, 3 = 1-day, 4 = live notice sent.</summary>
    int FeeNoticeStage = 0,
    DateTimeOffset? FeeNoticeSentAtUtc = null,
    /// <summary>True when customers and artisans are paying their own fees right now.</summary>
    bool UsersBearFees = false,
    string? UpdatedByName = null);

/// <summary>Update the platform settings (PUT /api/v1/admin/settings). All fields required.</summary>
public sealed record UpdatePlatformSettingsRequest(
    decimal CommissionRate,
    decimal EmergencyCommissionRate,
    int AutoConfirmHours,
    int MinWithdrawalNaira,
    int ReferralRewardNaira,
    int MaxCommissionDebtNaira,
    bool RequireGuarantors = false,
    int RequiredGuarantorCount = 2,
    /// <summary>Fee schedule. Omitted (null) keeps the stored values so older admin builds still save.</summary>
    FeeSettingsRequest? Fees = null);

/// <summary>The transaction-fee levers (part of PUT /api/v1/admin/settings).</summary>
public sealed record FeeSettingsRequest(
    DateTimeOffset? FeesStartAtUtc,
    decimal CardFeeRate,
    int CardFeeFlatNaira,
    int CardFeeFlatFromNaira,
    int CardFeeCapNaira,
    int TransferFeeTier1Naira,
    int TransferFeeTier1MaxNaira,
    int TransferFeeTier2Naira,
    int TransferFeeTier2MaxNaira,
    int TransferFeeTier3Naira);
