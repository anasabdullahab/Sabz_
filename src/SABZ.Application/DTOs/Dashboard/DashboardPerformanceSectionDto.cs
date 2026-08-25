using SABZ.Application.DTOs.Performance;

namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Farm performance snapshot reusing Prompt 11 (Prompt 12 dashboard section).
/// Ranking behavior is unchanged: only crops with recorded transactions are
/// ever ranked and wording stays factual - "best recorded crop" / "weakest
/// recorded crop", never "most profitable" or real-world claims.
/// </summary>
public class DashboardPerformanceSectionDto
{
    /// <summary>Existing Prompt 11 status: NoRecordedData, LimitedRecordedData, RecordedActivityAvailable.</summary>
    public string OverallStatus { get; set; } = string.Empty;

    /// <summary>Existing factual Prompt 11 status explanation.</summary>
    public string StatusExplanation { get; set; } = string.Empty;

    /// <summary>Recorded net result over the farm's full history.</summary>
    public decimal NetResult { get; set; }

    /// <summary>Crop with the best recorded net result; null when nothing is recorded.</summary>
    public RecordedCropPerformanceDto? BestRecordedCrop { get; set; }

    /// <summary>Crop with the weakest recorded net result; null when nothing is recorded.</summary>
    public RecordedCropPerformanceDto? WeakestRecordedCrop { get; set; }
}
