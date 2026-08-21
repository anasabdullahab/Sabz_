using SABZ.Application.DTOs.Monitoring;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Smart crop monitoring schedule (Prompt 7): rule-driven check generation,
/// due/upcoming views, and farmer completion/skip with observations.
/// Ownership is always derived from the authenticated user (JWT).
/// </summary>
public interface IMonitoringService
{
    /// <summary>All monitoring checks of one crop (ownership verified via crop -> farm).</summary>
    Task<List<MonitoringCheckDto>> GetChecksForCropAsync(Guid userId, Guid cropId, CancellationToken ct = default);

    /// <summary>The user's due checks (scheduled date reached, not completed/skipped), most overdue first.</summary>
    Task<List<MonitoringCheckDto>> GetDueChecksAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The user's upcoming checks (scheduled date in the future, not completed/skipped).</summary>
    Task<List<MonitoringCheckDto>> GetUpcomingChecksAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Complete a check with a controlled observation (Normal / SomethingSuspicious).
    /// Suspicious observations recommend (never trigger) Prompt 6 photo analysis.
    /// </summary>
    Task<MonitoringCompletionResponseDto> CompleteCheckAsync(Guid userId, Guid checkId, CompleteMonitoringCheckRequestDto request, CancellationToken ct = default);

    /// <summary>Skip a check. A skipped check never appears as due afterwards.</summary>
    Task<MonitoringCheckDto> SkipCheckAsync(Guid userId, Guid checkId, SkipMonitoringCheckRequestDto? request = null, CancellationToken ct = default);

    /// <summary>
    /// Idempotent check generation for a crop from applicable rules.
    /// Safe for crops created before Prompt 7, crops without a planting date
    /// and crops with no applicable rules.
    /// </summary>
    Task<MonitoringGenerationResultDto> EnsureChecksForCropAsync(Guid userId, Guid cropId, CancellationToken ct = default);
}
