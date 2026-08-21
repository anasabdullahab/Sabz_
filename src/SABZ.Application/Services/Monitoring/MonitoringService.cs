using Microsoft.Extensions.Logging;
using SABZ.Application.DTOs.Monitoring;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Monitoring;

/// <summary>
/// Prompt 7 monitoring schedule service.
///
/// Design decisions:
/// - Persisted check status is Scheduled/Completed/Skipped. "Upcoming" vs "Due"
///   is computed against a centralised UTC clock, so checks are never
///   auto-completed and skipped checks can never reappear as due.
/// - Generation is idempotent: one check per (crop, rule); a unique database
///   index backs this and the service also pre-checks existing rule ids.
/// - Ownership is always derived from the JWT user via crop -> farm.
/// - A suspicious observation only RECOMMENDS the existing Prompt 6 photo
///   workflow; it never calls the AI provider itself.
/// </summary>
public class MonitoringService : IMonitoringService
{
    public const string StatusUpcoming = "Upcoming";
    public const string StatusDue = "Due";
    public const string StatusCompleted = "Completed";
    public const string StatusSkipped = "Skipped";

    private const string ObservationNote =
        "This observation records what the farmer reported during the monitoring check. It is not a disease diagnosis.";

    private const int MaxNotesLength = 1000;

    private readonly ICropRepository _cropRepository;
    private readonly ICropMonitoringRuleRepository _ruleRepository;
    private readonly ICropMonitoringCheckRepository _checkRepository;
    private readonly ISystemClock _clock;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MonitoringService> _logger;

    public MonitoringService(
        ICropRepository cropRepository,
        ICropMonitoringRuleRepository ruleRepository,
        ICropMonitoringCheckRepository checkRepository,
        ISystemClock clock,
        INotificationService notificationService,
        ILogger<MonitoringService> logger)
    {
        _cropRepository = cropRepository;
        _ruleRepository = ruleRepository;
        _checkRepository = checkRepository;
        _clock = clock;
        _notificationService = notificationService;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    //  Read endpoints
    // ------------------------------------------------------------------

    public async Task<List<MonitoringCheckDto>> GetChecksForCropAsync(Guid userId, Guid cropId, CancellationToken ct = default)
    {
        await GetOwnedCropAsync(userId, cropId, ct);

        var checks = await _checkRepository.GetByCropIdAsync(cropId, ct);
        return checks.Select(c => MapToDto(c, _clock.UtcNow)).ToList();
    }

    public async Task<List<MonitoringCheckDto>> GetDueChecksAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var checks = await _checkRepository.GetByUserIdAsync(userId, ct);
        var dueChecks = checks
            .Where(c => c.Status == MonitoringCheckStatus.Scheduled && c.ScheduledDate <= now)
            .OrderBy(c => c.ScheduledDate)
            .ToList();

        // Prompt 8: lazy, idempotent creation of MonitoringDue notifications for
        // the due checks. Notification failures must never break the monitoring
        // read path or change monitoring state.
        try
        {
            await _notificationService.EnsureDueNotificationsAsync(dueChecks, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Due-notification generation failed for user {UserId}; due checks still returned.", userId);
        }

        return dueChecks
            .Select(c => MapToDto(c, now))
            .ToList();
    }

    public async Task<List<MonitoringCheckDto>> GetUpcomingChecksAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var checks = await _checkRepository.GetByUserIdAsync(userId, ct);

        return checks
            .Where(c => c.Status == MonitoringCheckStatus.Scheduled && c.ScheduledDate > now)
            .OrderBy(c => c.ScheduledDate)
            .Select(c => MapToDto(c, now))
            .ToList();
    }

    // ------------------------------------------------------------------
    //  Farmer actions
    // ------------------------------------------------------------------

    public async Task<MonitoringCompletionResponseDto> CompleteCheckAsync(
        Guid userId, Guid checkId, CompleteMonitoringCheckRequestDto request, CancellationToken ct = default)
    {
        var check = await GetOwnedCheckAsync(userId, checkId, ct);

        if (!Enum.TryParse<MonitoringObservation>(request.Observation, ignoreCase: true, out var observation))
            throw new ValidationException(
                "Observation must be exactly one of: Normal, SomethingSuspicious.");

        ValidateNotes(request.Notes);

        if (check.Status == MonitoringCheckStatus.Completed)
            throw new ConflictException("This monitoring check has already been completed.");

        if (check.Status == MonitoringCheckStatus.Skipped)
            throw new ConflictException("This monitoring check was skipped and can no longer be completed.");

        var now = _clock.UtcNow;
        check.Status = MonitoringCheckStatus.Completed;
        check.Observation = observation;
        check.FarmerNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        check.CompletedAt = now;

        _checkRepository.Update(check);
        await _checkRepository.SaveChangesAsync(ct);

        var suspicious = observation == MonitoringObservation.SomethingSuspicious;
        return new MonitoringCompletionResponseDto
        {
            Check = MapToDto(check, now),
            PhotoAnalysisRecommended = suspicious,
            NextAction = suspicious
                ? "Upload a clear photo of the affected crop or leaf for AI analysis using the crop's disease-detection endpoint."
                : "No further action is needed right now. Continue with the next scheduled monitoring check.",
            ObservationNote = ObservationNote
        };
    }

    public async Task<MonitoringCheckDto> SkipCheckAsync(
        Guid userId, Guid checkId, SkipMonitoringCheckRequestDto? request = null, CancellationToken ct = default)
    {
        var check = await GetOwnedCheckAsync(userId, checkId, ct);

        ValidateNotes(request?.Notes);

        if (check.Status == MonitoringCheckStatus.Completed)
            throw new ConflictException("This monitoring check has already been completed and cannot be skipped.");

        if (check.Status == MonitoringCheckStatus.Skipped)
            throw new ConflictException("This monitoring check has already been skipped.");

        var now = _clock.UtcNow;
        check.Status = MonitoringCheckStatus.Skipped;
        check.FarmerNotes = string.IsNullOrWhiteSpace(request?.Notes) ? null : request!.Notes!.Trim();
        check.SkippedAt = now;

        _checkRepository.Update(check);
        await _checkRepository.SaveChangesAsync(ct);

        return MapToDto(check, now);
    }

    // ------------------------------------------------------------------
    //  Idempotent generation
    // ------------------------------------------------------------------

    public async Task<MonitoringGenerationResultDto> EnsureChecksForCropAsync(Guid userId, Guid cropId, CancellationToken ct = default)
    {
        var crop = await GetOwnedCropAsync(userId, cropId, ct);

        var result = new MonitoringGenerationResultDto
        {
            CropId = crop.Id,
            PlantingDate = crop.PlantingDate,
            HasPlantingDate = crop.PlantingDate.HasValue
        };

        var before = await _checkRepository.GetByCropIdAsync(cropId, ct);
        result.ExistingChecks = before.Count;

        if (!crop.PlantingDate.HasValue)
        {
            result.Notes.Add(
                "This crop has no planting date, so no monitoring checks were generated. " +
                "Set a planting date and run generation again.");
            result.Checks = before.Select(c => MapToDto(c, _clock.UtcNow)).ToList();
            return result;
        }

        var rules = await _ruleRepository.GetActiveScheduledForCropAsync(crop.CropCatalogId, ct);
        result.RulesApplied = rules.Count;

        if (rules.Count == 0)
        {
            result.Notes.Add("No active monitoring rules are available for this crop yet.");
            result.Checks = before.Select(c => MapToDto(c, _clock.UtcNow)).ToList();
            return result;
        }

        // Idempotency: one check per (crop, rule). The database also enforces
        // a unique index on (CropId, RuleId) as a second safety net.
        var existingRuleIds = await _checkRepository.GetExistingRuleIdsAsync(cropId, ct);
        var plantingDate = crop.PlantingDate.Value.Date;

        foreach (var rule in rules.Where(r => !existingRuleIds.Contains(r.Id)))
        {
            await _checkRepository.AddAsync(new CropMonitoringCheck
            {
                Id = Guid.NewGuid(),
                CropId = crop.Id,
                RuleId = rule.Id,
                FarmId = crop.FarmId,
                ScheduledDate = plantingDate.AddDays(rule.DayOffsetAfterPlanting),
                Status = MonitoringCheckStatus.Scheduled,
                Title = rule.Title,
                Description = rule.Description,
                InspectionItems = rule.InspectionItems,
                Priority = rule.Priority,
                CreatedAt = _clock.UtcNow
            }, ct);
            result.ChecksCreated++;
        }

        if (result.ChecksCreated > 0)
            await _checkRepository.SaveChangesAsync(ct);
        else if (before.Count > 0)
            result.Notes.Add("Monitoring checks already exist for this crop; nothing was duplicated.");

        var after = await _checkRepository.GetByCropIdAsync(cropId, ct);
        result.Checks = after.Select(c => MapToDto(c, _clock.UtcNow)).ToList();
        return result;
    }

    // ------------------------------------------------------------------
    //  Ownership (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<Crop> GetOwnedCropAsync(Guid userId, Guid cropId, CancellationToken ct)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
            ?? throw new NotFoundException("Crop not found.");

        if (crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this crop.");

        return crop;
    }

    private async Task<CropMonitoringCheck> GetOwnedCheckAsync(Guid userId, Guid checkId, CancellationToken ct)
    {
        var check = await _checkRepository.GetByIdAsync(checkId, ct)
            ?? throw new NotFoundException("Monitoring check not found.");

        if (check.Crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this monitoring check.");

        return check;
    }

    // ------------------------------------------------------------------
    //  Mapping
    // ------------------------------------------------------------------

    private static void ValidateNotes(string? notes)
    {
        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > MaxNotesLength)
            throw new ValidationException($"Notes must be {MaxNotesLength} characters or fewer.");
    }

    private static MonitoringCheckDto MapToDto(CropMonitoringCheck check, DateTime nowUtc)
    {
        var status = check.Status switch
        {
            MonitoringCheckStatus.Completed => StatusCompleted,
            MonitoringCheckStatus.Skipped => StatusSkipped,
            _ => check.ScheduledDate <= nowUtc ? StatusDue : StatusUpcoming
        };

        return new MonitoringCheckDto
        {
            Id = check.Id,
            CropId = check.CropId,
            CropName = check.Crop?.CropName ?? string.Empty,
            CropCatalogName = check.Crop?.CropCatalog?.Name,
            FarmId = check.FarmId,
            FarmName = check.Crop?.Farm?.FarmName,
            ScheduledDate = check.ScheduledDate,
            Status = status,
            Title = check.Title,
            Description = check.Description,
            InspectionItems = SplitList(check.InspectionItems),
            Priority = check.Priority,
            Observation = check.Observation?.ToString(),
            FarmerNotes = check.FarmerNotes,
            CompletedAt = check.CompletedAt,
            SkippedAt = check.SkippedAt,
            PhotoAnalysisRecommended =
                check.Status == MonitoringCheckStatus.Completed
                && check.Observation == MonitoringObservation.SomethingSuspicious
        };
    }

    private static List<string> SplitList(string semicolonSeparated)
        => semicolonSeparated
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
