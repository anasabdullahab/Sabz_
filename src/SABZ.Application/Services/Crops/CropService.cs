using Microsoft.Extensions.Logging;
using SABZ.Application.DTOs.Crops;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Crops;

public class CropService : ICropService
{
    private readonly ICropRepository _cropRepository;
    private readonly IFarmRepository _farmRepository;
    private readonly ICropMonitoringRuleRepository _monitoringRuleRepository;
    private readonly ICropMonitoringCheckRepository _monitoringCheckRepository;
    private readonly IFinancialTransactionRepository _financialTransactionRepository;
    private readonly INotificationService _notificationService;
    private readonly ISystemClock _clock;
    private readonly ILogger<CropService> _logger;

    private static readonly HashSet<string> ValidSeasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rabi", "Kharif", "Other"
    };

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "Harvested", "Failed", "Planned"
    };

    private static readonly HashSet<string> ValidGrowthStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sowing", "Germination", "Vegetative", "Flowering", "Fruiting", "Maturity", "Harvesting"
    };

    public CropService(
        ICropRepository cropRepository,
        IFarmRepository farmRepository,
        ICropMonitoringRuleRepository monitoringRuleRepository,
        ICropMonitoringCheckRepository monitoringCheckRepository,
        IFinancialTransactionRepository financialTransactionRepository,
        INotificationService notificationService,
        ISystemClock clock,
        ILogger<CropService> logger)
    {
        _cropRepository = cropRepository;
        _farmRepository = farmRepository;
        _monitoringRuleRepository = monitoringRuleRepository;
        _monitoringCheckRepository = monitoringCheckRepository;
        _financialTransactionRepository = financialTransactionRepository;
        _notificationService = notificationService;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CropResponseDto> CreateCropAsync(Guid userId, Guid farmId, CreateCropDto dto)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
                   ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        ValidateSeason(dto.Season);
        if (!string.IsNullOrWhiteSpace(dto.GrowthStage))
            ValidateGrowthStage(dto.GrowthStage);

        var status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status;
        ValidateStatus(status);

        // Link the crop to the catalog by name when the client omits the id so
        // monitoring rules (keyed by CropCatalogId) can match. Unknown names
        // simply stay unlinked - custom crops are still supported.
        var catalogId = dto.CropCatalogId ?? await _cropRepository.FindCatalogIdByNameAsync(dto.CropName);

        var crop = new Crop
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            CropCatalogId = catalogId,
            CropName = dto.CropName,
            Season = dto.Season,
            PlantingDate = dto.PlantingDate,
            HarvestDate = dto.HarvestDate,
            GrowthStage = dto.GrowthStage,
            PreviousCrop = dto.PreviousCrop,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        await _cropRepository.AddAsync(crop);
        await _cropRepository.SaveChangesAsync();

        // Prompt 7: schedule monitoring checks for crops with a planting date.
        // Degrades gracefully - crop creation must never fail because of monitoring.
        await TryGenerateMonitoringChecksAsync(crop);

        // Prompt 8: when some of the newly generated checks are already due
        // (past planting date), create their notifications immediately instead
        // of waiting for the next monitoring read.
        await TryGenerateDueNotificationsAsync(crop.Id);

        return MapToResponse(crop);
    }

    /// <summary>
    /// Idempotent monitoring-check generation (one check per rule, keyed by the
    /// unique CropId+RuleId database index). Runs in the same save scope; when
    /// the crop has no planting date or no applicable rules it simply does nothing.
    /// </summary>
    private async Task TryGenerateMonitoringChecksAsync(Crop crop)
    {
        try
        {
            if (!crop.PlantingDate.HasValue)
                return;

            var rules = await _monitoringRuleRepository.GetActiveScheduledForCropAsync(crop.CropCatalogId);
            if (rules.Count == 0)
                return;

            var existingRuleIds = await _monitoringCheckRepository.GetExistingRuleIdsAsync(crop.Id);
            var plantingDate = crop.PlantingDate.Value.Date;

            foreach (var rule in rules.Where(r => !existingRuleIds.Contains(r.Id)))
            {
                await _monitoringCheckRepository.AddAsync(new CropMonitoringCheck
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
                });
            }

            await _monitoringCheckRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Monitoring is additive enrichment - never break existing crop creation.
            _logger.LogWarning(ex, "Monitoring check generation skipped for crop {CropId}.", crop.Id);
        }
    }

    /// <summary>
    /// Idempotent notification pass for one crop's checks: loads the checks with
    /// ownership context and hands the already-due ones to the notification
    /// service. Failures are swallowed - crop creation must never break.
    /// </summary>
    private async Task TryGenerateDueNotificationsAsync(Guid cropId)
    {
        try
        {
            var checks = await _monitoringCheckRepository.GetByCropIdAsync(cropId);
            var now = _clock.UtcNow;
            var due = checks
                .Where(c => c.Status == MonitoringCheckStatus.Scheduled && c.ScheduledDate <= now)
                .ToList();
            if (due.Count > 0)
                await _notificationService.EnsureDueNotificationsAsync(due);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Due-notification generation skipped for crop {CropId}.", cropId);
        }
    }

    public async Task<List<CropResponseDto>> GetCropsByFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
                   ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        var crops = await _cropRepository.GetByFarmIdAsync(farmId);
        return crops.Select(MapToResponse).ToList();
    }

    public async Task<CropResponseDto> GetCropByIdAsync(Guid userId, Guid cropId)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
                   ?? throw new NotFoundException("Crop not found.");

        if (crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this crop.");

        return MapToResponse(crop);
    }

    public async Task<CropResponseDto> UpdateCropAsync(Guid userId, Guid cropId, UpdateCropDto dto)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
                   ?? throw new NotFoundException("Crop not found.");

        if (crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this crop.");

        ValidateSeason(dto.Season);
        if (!string.IsNullOrWhiteSpace(dto.GrowthStage))
            ValidateGrowthStage(dto.GrowthStage);

        var status = string.IsNullOrWhiteSpace(dto.Status) ? crop.Status : dto.Status;
        ValidateStatus(status);

        crop.CropName = dto.CropName;
        crop.CropCatalogId = dto.CropCatalogId ?? await _cropRepository.FindCatalogIdByNameAsync(dto.CropName);
        crop.Season = dto.Season;
        crop.PlantingDate = dto.PlantingDate;
        crop.HarvestDate = dto.HarvestDate;
        crop.GrowthStage = dto.GrowthStage;
        crop.PreviousCrop = dto.PreviousCrop;
        crop.Status = status;
        crop.UpdatedAt = DateTime.UtcNow;

        _cropRepository.Update(crop);
        await _cropRepository.SaveChangesAsync();

        return MapToResponse(crop);
    }

    public async Task DeleteCropAsync(Guid userId, Guid cropId)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
                   ?? throw new NotFoundException("Crop not found.");

        if (crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this crop.");

        // Prompt 9: keep financial history, drop the crop link (application-level
        // SetNull - the database FK is Restrict). Saved atomically with the crop
        // removal through the shared scoped DbContext.
        await _financialTransactionRepository.NullifyCropReferencesAsync(crop.Id);

        _cropRepository.Remove(crop);
        await _cropRepository.SaveChangesAsync();
    }

    private static void ValidateSeason(string season)
    {
        if (!ValidSeasons.Contains(season))
            throw new Domain.Exceptions.ValidationException($"Season must be one of: {string.Join(", ", ValidSeasons)}.");
    }

    private static void ValidateStatus(string status)
    {
        if (!ValidStatuses.Contains(status))
            throw new Domain.Exceptions.ValidationException($"Status must be one of: {string.Join(", ", ValidStatuses)}.");
    }

    private static void ValidateGrowthStage(string growthStage)
    {
        if (!ValidGrowthStages.Contains(growthStage))
            throw new Domain.Exceptions.ValidationException($"Growth stage must be one of: {string.Join(", ", ValidGrowthStages)}.");
    }

    private static CropResponseDto MapToResponse(Crop crop)
    {
        return new CropResponseDto
        {
            Id = crop.Id,
            FarmId = crop.FarmId,
            CropCatalogId = crop.CropCatalogId,
            CropName = crop.CropName,
            Season = crop.Season,
            PlantingDate = crop.PlantingDate,
            HarvestDate = crop.HarvestDate,
            GrowthStage = crop.GrowthStage,
            PreviousCrop = crop.PreviousCrop,
            Status = crop.Status,
            CreatedAt = crop.CreatedAt,
            UpdatedAt = crop.UpdatedAt
        };
    }
}
