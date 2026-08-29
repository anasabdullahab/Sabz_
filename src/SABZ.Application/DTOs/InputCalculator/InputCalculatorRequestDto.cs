using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.InputCalculator;

/// <summary>
/// Precision input &amp; dosage calculator request (Prompt 16).
///
/// Deliberately minimal: the authoritative farm area comes from the Farm
/// record server-side, so the client never supplies an area, an owner id, a
/// formula or a rate lookup hint. The dosage rate used is exactly the one
/// supplied here - SABZ never invents, retrieves or adjusts it.
/// </summary>
public class InputCalculatorRequestDto
{
    /// <summary>Optional crop on the selected farm this calculation refers to.</summary>
    public Guid? CropId { get; set; }

    [Required(ErrorMessage = "Input name is required.")]
    public string? InputName { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    public string? Category { get; set; }

    public decimal DosageRate { get; set; }

    [Required(ErrorMessage = "Dosage unit is required.")]
    public string? DosageUnit { get; set; }

    [Required(ErrorMessage = "Dosage basis is required.")]
    public string? DosageBasis { get; set; }
}
