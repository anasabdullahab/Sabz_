using System.Globalization;
using SABZ.Application.DTOs.InputCalculator;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.InputCalculator;

/// <summary>
/// Precision crop input &amp; dosage calculator (Prompt 16).
///
/// Farm Area × Dosage Rate = Required Quantity - nothing more. The rate is
/// always exactly the one supplied by the farmer; SABZ never invents,
/// retrieves, prescribes or AI-generates rates. The calculation is pure
/// decimal arithmetic performed locally: no AI provider, no external call,
/// no persistence, no notifications, no financial interaction.
///
/// Area conversion (only when the farm's stored unit differs from the dosage
/// basis) uses the fixed, documented constants
/// 1 hectare = 2.47105 acres and 1 acre = 0.404685642 hectares; intermediate
/// values are never rounded, only the final displayed quantity is.
/// </summary>
public class InputCalculatorService : IInputCalculatorService
{
    /// <summary>Documented, deterministic conversion constants.</summary>
    public const decimal HectareToAcreFactor = 2.47105m;
    public const decimal AcreToHectareFactor = 0.404685642m;

    private const int MaxInputNameLength = 150;

    /// <summary>
    /// Reasonable ceiling for any per-area agricultural rate (kg, L, g or ml
    /// per acre/hectare). Rates beyond this are data-entry errors, not
    /// agronomic judgement calls.
    /// </summary>
    private const decimal MaxDosageRate = 100_000m;

    private const string Disclaimer =
        "The calculation only applies the supplied dosage rate to the supplied farm area. " +
        "It does not determine whether the rate is appropriate for a specific crop, pest, " +
        "disease, product, formulation, or local regulation. Always follow the product label " +
        "and applicable agricultural guidance.";

    private readonly IFarmRepository _farmRepository;
    private readonly ICropRepository _cropRepository;

    public InputCalculatorService(IFarmRepository farmRepository, ICropRepository cropRepository)
    {
        _farmRepository = farmRepository;
        _cropRepository = cropRepository;
    }

    public async Task<InputCalculatorResponseDto> CalculateAsync(
        Guid userId, Guid farmId, InputCalculatorRequestDto dto, CancellationToken ct = default)
    {
        var inputName = ValidateInputName(dto.InputName);
        var category = NormalizeControlled(dto.Category, InputCalculatorCategories.All, "Category");
        var dosageUnit = NormalizeControlled(dto.DosageUnit, InputCalculatorQuantityUnits.All, "Dosage unit");
        var dosageBasis = NormalizeControlled(dto.DosageBasis, InputCalculatorDosageBases.All, "Dosage basis");
        var dosageRate = ValidateDosageRate(dto.DosageRate);

        // Existing SABZ ownership pattern: 404 unknown farm, 403 foreign farm.
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        // Optional crop reference: must exist and must belong to this farm.
        // Never trust client-supplied ownership fields.
        if (dto.CropId is Guid cropId)
        {
            var crop = await _cropRepository.GetByIdAsync(cropId)
                ?? throw new NotFoundException("Crop not found.");

            if (crop.FarmId != farmId)
                throw new ValidationException("The selected crop does not belong to the selected farm.");
        }

        var farmArea = ValidateFarmArea(farm);
        var farmAreaUnit = NormalizeFarmAreaUnit(farm.FarmSizeUnit);

        var targetAreaUnit = dosageBasis == InputCalculatorDosageBases.PerAcre
            ? InputCalculatorAreaUnits.Acres
            : InputCalculatorAreaUnits.Hectares;

        // Convert the farm area into the dosage basis only when necessary;
        // intermediate values stay unrounded.
        decimal calculationArea;
        bool conversionApplied;
        if (farmAreaUnit == targetAreaUnit)
        {
            calculationArea = farmArea;
            conversionApplied = false;
        }
        else
        {
            calculationArea = targetAreaUnit == InputCalculatorAreaUnits.Acres
                ? farmArea * HectareToAcreFactor
                : farmArea * AcreToHectareFactor;
            conversionApplied = true;
        }

        // The result is expressed in the dosage unit itself: the calculator
        // never converts between incompatible physical units (kg stays kg,
        // liters stay liters).
        var requiredQuantity = Math.Round(calculationArea * dosageRate, 2, MidpointRounding.AwayFromZero);

        var basisNoun = targetAreaUnit == InputCalculatorAreaUnits.Acres ? "acre" : "hectare";
        var formula = string.Create(CultureInfo.InvariantCulture,
            $"{Format(calculationArea)} {targetAreaUnit} \u00d7 {Format(dosageRate)} {dosageUnit}/{basisNoun} = {Format(requiredQuantity)} {dosageUnit}");

        return new InputCalculatorResponseDto
        {
            FarmId = farm.Id,
            CropId = dto.CropId,
            InputName = inputName,
            Category = category,
            FarmArea = farmArea,
            FarmAreaUnit = farmAreaUnit,
            CalculationArea = calculationArea,
            CalculationAreaUnit = targetAreaUnit,
            DosageRate = dosageRate,
            DosageUnit = dosageUnit,
            DosageBasis = dosageBasis,
            RequiredQuantity = requiredQuantity,
            RequiredQuantityUnit = dosageUnit,
            ConversionApplied = conversionApplied,
            CalculationFormula = formula,
            Disclaimer = Disclaimer
        };
    }

    // ------------------------------------------------------------------
    //  Validation helpers
    // ------------------------------------------------------------------

    private static string ValidateInputName(string? inputName)
    {
        var trimmed = inputName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException("Input name is required.");
        if (trimmed.Length > MaxInputNameLength)
            throw new ValidationException($"Input name must be at most {MaxInputNameLength} characters.");
        return trimmed;
    }

    private static decimal ValidateDosageRate(decimal dosageRate)
    {
        if (dosageRate <= 0m)
            throw new ValidationException("Dosage rate must be greater than zero.");
        if (dosageRate > MaxDosageRate)
            throw new ValidationException($"Dosage rate must not exceed {MaxDosageRate.ToString(CultureInfo.InvariantCulture)}.");
        return dosageRate;
    }

    /// <summary>
    /// The authoritative area comes from the Farm record; a farm without a
    /// positive recorded area cannot be calculated.
    /// </summary>
    private static decimal ValidateFarmArea(Farm farm)
    {
        if (farm.FarmSize <= 0m)
            throw new ValidationException("The farm does not have a usable recorded area.");
        return farm.FarmSize;
    }

    /// <summary>
    /// Interprets the farm's stored size unit. Only Acres/Hectares are
    /// understood; anything else is rejected instead of being guessed.
    /// </summary>
    private static string NormalizeFarmAreaUnit(string farmSizeUnit)
    {
        var trimmed = farmSizeUnit?.Trim();
        if (string.Equals(trimmed, "Acre", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, InputCalculatorAreaUnits.Acres, StringComparison.OrdinalIgnoreCase))
            return InputCalculatorAreaUnits.Acres;

        if (string.Equals(trimmed, "Hectare", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, InputCalculatorAreaUnits.Hectares, StringComparison.OrdinalIgnoreCase))
            return InputCalculatorAreaUnits.Hectares;

        throw new ValidationException(
            $"Farm size unit '{farmSizeUnit}' is not supported by the input calculator. Supported units: Acres, Hectares.");
    }

    /// <summary>Case-insensitive membership check, canonical value returned.</summary>
    private static string NormalizeControlled(string? value, HashSet<string> allowed, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException($"{fieldName} is required.");

        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        throw new ValidationException(
            $"{fieldName} '{trimmed}' is not supported. Supported values: {string.Join(", ", allowed)}.");
    }

    /// <summary>Culture-invariant display without trailing zeros.</summary>
    private static string Format(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);
}
