namespace SABZ.Application.DTOs.InputCalculator;

/// <summary>
/// Precision input &amp; dosage calculator result (Prompt 16). Pure echo of the
/// supplied inputs plus the deterministic arithmetic - nothing here is an
/// agronomic recommendation. No user/owner identifiers are ever exposed.
/// </summary>
public class InputCalculatorResponseDto
{
    public Guid FarmId { get; set; }
    public Guid? CropId { get; set; }

    public string InputName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    /// <summary>Authoritative area stored on the Farm record.</summary>
    public decimal FarmArea { get; set; }
    public string FarmAreaUnit { get; set; } = string.Empty;

    /// <summary>
    /// Farm area expressed in the dosage-basis unit (converted only when the
    /// farm's stored unit differs from the dosage basis). Intermediate values
    /// are never rounded; only <see cref="RequiredQuantity"/> is rounded.
    /// </summary>
    public decimal CalculationArea { get; set; }
    public string CalculationAreaUnit { get; set; } = string.Empty;

    public decimal DosageRate { get; set; }
    public string DosageUnit { get; set; } = string.Empty;
    public string DosageBasis { get; set; } = string.Empty;

    /// <summary>
    /// Calculated total quantity. Always expressed in the same unit as the
    /// dosage - the calculator never converts between incompatible physical
    /// units (e.g. kilograms are never turned into liters).
    /// </summary>
    public decimal RequiredQuantity { get; set; }
    public string RequiredQuantityUnit { get; set; } = string.Empty;

    public bool ConversionApplied { get; set; }

    /// <summary>Human-readable, deterministic formula echo.</summary>
    public string CalculationFormula { get; set; } = string.Empty;

    public string Disclaimer { get; set; } = string.Empty;
}
