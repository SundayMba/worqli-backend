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
    int RequiredGuarantorCount = 2);

/// <summary>Update the platform settings (PUT /api/v1/admin/settings). All fields required.</summary>
public sealed record UpdatePlatformSettingsRequest(
    decimal CommissionRate,
    decimal EmergencyCommissionRate,
    int AutoConfirmHours,
    int MinWithdrawalNaira,
    int ReferralRewardNaira,
    int MaxCommissionDebtNaira,
    bool RequireGuarantors = false,
    int RequiredGuarantorCount = 2);
