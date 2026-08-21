namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// Financial record completeness / data readiness (Prompt 10). This measures
/// ONLY how much financial data has been entered into SABZ - it is not a
/// credit score, loan score, insurance score, or approval of any kind.
/// Computed from the farm's full transaction history; never persisted.
/// </summary>
public class FinancialCompletenessDto
{
    public Guid FarmId { get; set; }

    /// <summary>NoData, Partial, or Complete.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>0-100: passed checks x 20.</summary>
    public int Score { get; set; }

    public string Explanation { get; set; } = string.Empty;

    /// <summary>What this score does NOT measure.</summary>
    public List<string> Limitations { get; set; } = [];

    /// <summary>Always: "Based only on transactions entered into SABZ."</summary>
    public string Disclaimer { get; set; } = string.Empty;

    /// <summary>The five deterministic checks, each worth 20 points.</summary>
    public List<FinancialCompletenessCheckDto> Checks { get; set; } = [];
}
