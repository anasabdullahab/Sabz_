namespace SABZ.Application.DTOs.Financial;

/// <summary>One deterministic financial-data-completeness check (each worth 20 points).</summary>
public class FinancialCompletenessCheckDto
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Description { get; set; } = string.Empty;
}
