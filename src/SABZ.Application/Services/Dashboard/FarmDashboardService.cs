using Microsoft.Extensions.Logging;
using SABZ.Application.DTOs.Dashboard;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Dashboard;

/// <summary>
/// Unified Farm Dashboard &amp; Insights (Prompt 12).
///
/// Design decisions:
/// - Pure aggregation/orchestration over EXISTING features: farms/locations,
///   crops, Prompt 7 monitoring, Prompt 8 notifications, the Prompt 9 ledger,
///   Prompt 10 financial health, Prompt 11 performance and Prompt 3 weather.
///   No business logic is duplicated and nothing derived is persisted.
/// - Reading due checks goes through the existing Prompt 7 read path, so the
///   established Prompt 8 lazy/idempotent MonitoringDue notification behavior
///   is preserved - the dashboard never adds a second notification mechanism
///   and never changes monitoring state.
/// - Weather is reused from the existing Prompt 3 service only when the farm
///   has GPS coordinates; a weather failure can never break the dashboard.
///   External weather is always clearly distinguished from recorded SABZ data.
/// - Ownership follows the existing JWT user -> farm pattern; the client
///   never supplies a UserId. All wording is factual and deterministic.
/// </summary>
public class FarmDashboardService : IFarmDashboardService
{
    // Structured limitation codes (stable, factual).
    public const string LimitRecordedDataOnly = "RecordedDataOnly";
    public const string LimitNoCrops = "NoCrops";
    public const string LimitNoFinancialTransactions = "NoFinancialTransactions";
    public const string LimitNoCoordinates = "NoCoordinates";
    public const string LimitWeatherUnavailable = "WeatherUnavailable";

    /// <summary>Bounded "recent notifications" window (newest first).</summary>
    public const int RecentNotificationLimit = 5;

    public const string DashboardDisclaimer =
        "The SABZ Farm Dashboard combines information already recorded or calculated within SABZ into a unified view. " +
        "It does not independently verify real-world farm activity, predict future outcomes, determine farming skill, " +
        "creditworthiness, or financial eligibility.";

    public const string WeatherNote =
        "Weather is external data from the configured weather provider. It is not recorded by the farmer in SABZ.";

    private readonly IFarmRepository _farmRepository;
    private readonly ICropRepository _cropRepository;
    private readonly IMonitoringService _monitoringService;
    private readonly ICropMonitoringCheckRepository _checkRepository;
    private readonly INotificationService _notificationService;
    private readonly IFinancialService _financialService;
    private readonly IFinancialHealthService _financialHealthService;
    private readonly IFarmPerformanceService _farmPerformanceService;
    private readonly IWeatherService _weatherService;
    private readonly ISystemClock _clock;
    private readonly ILogger<FarmDashboardService> _logger;

    public FarmDashboardService(
        IFarmRepository farmRepository,
        ICropRepository cropRepository,
        IMonitoringService monitoringService,
        ICropMonitoringCheckRepository checkRepository,
        INotificationService notificationService,
        IFinancialService financialService,
        IFinancialHealthService financialHealthService,
        IFarmPerformanceService farmPerformanceService,
        IWeatherService weatherService,
        ISystemClock clock,
        ILogger<FarmDashboardService> logger)
    {
        _farmRepository = farmRepository;
        _cropRepository = cropRepository;
        _monitoringService = monitoringService;
        _checkRepository = checkRepository;
        _notificationService = notificationService;
        _financialService = financialService;
        _financialHealthService = financialHealthService;
        _farmPerformanceService = farmPerformanceService;
        _weatherService = weatherService;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FarmDashboardDto> GetDashboardAsync(Guid userId, Guid farmId, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var limitations = new List<DashboardLimitationDto>();

        // --------------------------------------------------------------
        //  Farm section (existing stored facts, never the owner's UserId)
        // --------------------------------------------------------------
        var farmSection = new DashboardFarmSectionDto
        {
            FarmId = farm.Id,
            FarmName = farm.FarmName,
            Province = farm.Province.Name,
            District = farm.District.Name,
            Tehsil = farm.Tehsil.Name,
            FarmSize = farm.FarmSize,
            FarmSizeUnit = farm.FarmSizeUnit,
            SoilType = farm.SoilType,
            IrrigationType = farm.IrrigationType,
            HasCoordinates = farm.Latitude.HasValue && farm.Longitude.HasValue
        };

        // --------------------------------------------------------------
        //  Crops section (existing crop records only)
        // --------------------------------------------------------------
        var crops = await _cropRepository.GetByFarmIdAsync(farm.Id);
        var cropsSection = new DashboardCropsSectionDto
        {
            TotalCrops = crops.Count,
            ActiveCrops = crops.Count(c => c.Status == "Active"),
            Crops = crops.Select(c => new DashboardCropItemDto
            {
                CropId = c.Id,
                CropName = c.CropName,
                Season = c.Season,
                GrowthStage = c.GrowthStage,
                Status = c.Status
            }).ToList()
        };

        if (crops.Count == 0)
            limitations.Add(new DashboardLimitationDto
            {
                Code = LimitNoCrops,
                Message = "This farm has no crop records in SABZ yet, so crop-related sections are empty."
            });

        // --------------------------------------------------------------
        //  Monitoring section (Prompt 7 reuse; read-only)
        //  The existing due read path keeps the established Prompt 8 lazy
        //  MonitoringDue notification behavior; nothing is duplicated here.
        // --------------------------------------------------------------
        var dueChecks = await _monitoringService.GetDueChecksAsync(userId, ct);
        var upcomingChecks = await _monitoringService.GetUpcomingChecksAsync(userId, ct);
        var checkEvents = await _checkRepository.GetFarmCheckEventsAsync(farm.Id, ct);

        var monitoringSection = new DashboardMonitoringSectionDto
        {
            DueChecks = dueChecks.Count(c => c.FarmId == farm.Id),
            UpcomingChecks = upcomingChecks.Count(c => c.FarmId == farm.Id),
            CompletedChecks = checkEvents.Count(e => e.Status == MonitoringCheckStatus.Completed),
            SkippedChecks = checkEvents.Count(e => e.Status == MonitoringCheckStatus.Skipped),
            TotalChecks = checkEvents.Count
        };

        // --------------------------------------------------------------
        //  Notifications section (Prompt 8 reuse; dashboard only reads)
        // --------------------------------------------------------------
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId, ct);
        var recent = await _notificationService.GetNotificationsAsync(userId, RecentNotificationLimit, ct);
        var notificationsSection = new DashboardNotificationsSectionDto
        {
            UnreadCount = unreadCount,
            RecentNotifications = recent
        };

        // --------------------------------------------------------------
        //  Financial section (Prompt 9 dynamic P&L summary, full history)
        // --------------------------------------------------------------
        var ledger = await _financialService.GetSummaryAsync(userId, farm.Id, cropId: null, fromDate: null, toDate: null, ct);
        var financialSection = new DashboardFinancialSectionDto
        {
            TotalIncome = ledger.TotalIncome,
            TotalExpenses = ledger.TotalExpenses,
            NetResult = ledger.NetProfitLoss,
            TransactionCount = ledger.TransactionCount
        };

        if (ledger.TransactionCount == 0)
            limitations.Add(new DashboardLimitationDto
            {
                Code = LimitNoFinancialTransactions,
                Message = "No financial transactions are recorded for this farm in SABZ, so all financial values are zero."
            });

        // --------------------------------------------------------------
        //  Financial health section (Prompt 10 reuse, disclaimers preserved)
        // --------------------------------------------------------------
        var health = await _financialHealthService.GetFarmHealthAsync(userId, farm.Id, fromDate: null, toDate: null, ct);
        var completeness = await _financialHealthService.GetCompletenessAsync(userId, farm.Id, ct);
        var financialHealthSection = new DashboardFinancialHealthSectionDto
        {
            HealthIndicator = health.HealthIndicator,
            HealthExplanation = health.HealthExplanation,
            CompletenessStatus = completeness.Status,
            CompletenessScore = completeness.Score,
            Disclaimer = completeness.Disclaimer
        };

        // --------------------------------------------------------------
        //  Performance section (Prompt 11 reuse, ranking unchanged)
        // --------------------------------------------------------------
        var performance = await _farmPerformanceService.GetPerformanceSummaryAsync(userId, farm.Id, fromDate: null, toDate: null, ct);
        var performanceSection = new DashboardPerformanceSectionDto
        {
            OverallStatus = performance.OverallStatus,
            StatusExplanation = performance.StatusExplanation,
            NetResult = performance.NetResult,
            BestRecordedCrop = performance.BestRecordedCrop,
            WeakestRecordedCrop = performance.WeakestRecordedCrop
        };

        // --------------------------------------------------------------
        //  Weather section (Prompt 3 reuse; external data, optional)
        //  A weather failure must never break the unified dashboard.
        // --------------------------------------------------------------
        DashboardWeatherSectionDto? weatherSection = null;
        if (farm.Latitude.HasValue && farm.Longitude.HasValue)
        {
            try
            {
                var weather = await _weatherService.GetCurrentWeatherAsync(userId, farm.Id, ct);
                weatherSection = new DashboardWeatherSectionDto
                {
                    Source = weather.Source,
                    RetrievedAt = weather.RetrievedAt,
                    Current = weather.Current,
                    Note = WeatherNote
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard weather unavailable for farm {FarmId}; dashboard returned without weather.", farm.Id);
                limitations.Add(new DashboardLimitationDto
                {
                    Code = LimitWeatherUnavailable,
                    Message = "Current weather could not be retrieved for this farm. The dashboard shows recorded SABZ data without weather."
                });
            }
        }
        else
        {
            limitations.Add(new DashboardLimitationDto
            {
                Code = LimitNoCoordinates,
                Message = "This farm has no GPS coordinates recorded, so external weather is not shown on the dashboard."
            });
        }

        // --------------------------------------------------------------
        //  Data-context limitation + disclaimer (always present)
        // --------------------------------------------------------------
        limitations.Insert(0, new DashboardLimitationDto
        {
            Code = LimitRecordedDataOnly,
            Message = "This dashboard reflects only information recorded or calculated in SABZ"
                + (weatherSection is not null ? ", plus external weather data from the configured provider" : string.Empty)
                + ". It does not measure real-world farm activity, farming skill, future outcomes, creditworthiness, or financial eligibility."
        });

        return new FarmDashboardDto
        {
            Farm = farmSection,
            Crops = cropsSection,
            Monitoring = monitoringSection,
            Notifications = notificationsSection,
            Financial = financialSection,
            FinancialHealth = financialHealthSection,
            Performance = performanceSection,
            Weather = weatherSection,
            Limitations = limitations,
            Disclaimer = DashboardDisclaimer,
            GeneratedAt = _clock.UtcNow
        };
    }

    // ------------------------------------------------------------------
    //  Ownership (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId, CancellationToken ct)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }
}
