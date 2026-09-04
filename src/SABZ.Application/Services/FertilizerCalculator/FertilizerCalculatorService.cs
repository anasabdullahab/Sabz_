using System.Globalization;
using SABZ.Application.DTOs.FertilizerCalculator;
using SABZ.Application.Services.CropKnowledge;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.FertilizerCalculator;

/// <summary>
/// Automated fertilizer presets engine (hackathon feature).
///
/// Inputs: crop name + farm size (acres) - nothing else. The engine reads
/// per-acre bag presets from the local crop knowledge base and produces an
/// exact bag-count schedule (DAP/SSP at sowing, Urea split across the first
/// two irrigations). 100% local arithmetic - no AI, no external calls.
/// </summary>
public class FertilizerCalculatorService
{
    private const string Disclaimer =
        "Standard per-acre recommendations from the SABZ local crop knowledge base. " +
        "Actual needs vary by soil fertility - always confirm with a soil test and " +
        "follow product label instructions.";

    public FertilizerCalculatorResponseDto Calculate(FertilizerCalculatorRequestDto dto)
    {
        var crop = CropKnowledgeBase.Find(dto.CropName)
            ?? throw new ValidationException(
                $"'{dto.CropName}' is not in the local crop knowledge base. Supported crops: " +
                $"{string.Join(", ", CropKnowledgeBase.Entries.Select(e => e.Name))}.");

        var acres = dto.FarmSizeAcres;
        if (acres <= 0m)
            throw new ValidationException("Farm size must be greater than zero.");

        var schedule = new List<FertilizerApplicationDto>();

        // Sowing: DAP + SSP (+ starter urea for some crops)
        var sowing = new FertilizerApplicationDto
        {
            Stage = "Sowing",
            Timing = "At sowing time (drill or broadcast before final land preparation)",
            Products = BuildProducts(crop.FertilizerPlan.Sowing, acres,
                sowing: true),
        };
        if (sowing.Products.Count > 0) schedule.Add(sowing);

        // First irrigation: urea top dress
        var first = new FertilizerApplicationDto
        {
            Stage = "First Irrigation",
            Timing = "21-30 days after sowing (top dress with first watering)",
            Products = BuildProducts(crop.FertilizerPlan.FirstIrrigation, acres, sowing: false),
        };
        if (first.Products.Count > 0) schedule.Add(first);

        // Second irrigation: urea top dress
        var second = new FertilizerApplicationDto
        {
            Stage = "Second Irrigation",
            Timing = "45-60 days after sowing (top dress with second watering)",
            Products = BuildProducts(crop.FertilizerPlan.SecondIrrigation, acres, sowing: false),
        };
        if (second.Products.Count > 0) schedule.Add(second);

        return new FertilizerCalculatorResponseDto
        {
            CropName = crop.Name,
            CropNameUrdu = crop.NameUrdu,
            FarmSizeAcres = acres,
            Schedule = schedule,
            Notes = $"{crop.Name} ({crop.NameUrdu}) typically matures in {crop.MaturityDays} days. " +
                    $"All bags are {CropKnowledgeBase.BagWeightKg.ToString("0", CultureInfo.InvariantCulture)} kg.",
            Disclaimer = Disclaimer,
        };
    }

    private static List<FertilizerProductDto> BuildProducts(
        FertilizerApplication plan, decimal acres, bool sowing)
    {
        var products = new List<FertilizerProductDto>();

        if (plan.Dap > 0m)
            products.Add(Product("DAP", plan.Dap, acres, sowing ? "Drill at sowing with seed" : "Broadcast before irrigation"));

        if (plan.Ssp > 0m)
            products.Add(Product("SSP", plan.Ssp, acres, sowing ? "Drill at sowing with seed" : "Broadcast before irrigation"));

        if (plan.Urea > 0m)
            products.Add(Product("Urea", plan.Urea, acres, "Top dress at irrigation"));

        return products;
    }

    private static FertilizerProductDto Product(string name, decimal bagsPerAcre, decimal acres, string application)
    {
        var totalBags = Math.Round(bagsPerAcre * acres, 1, MidpointRounding.AwayFromZero);
        return new FertilizerProductDto
        {
            Product = name,
            BagsPerAcre = bagsPerAcre,
            TotalBags = totalBags,
            TotalKg = Math.Round(totalBags * CropKnowledgeBase.BagWeightKg, 0, MidpointRounding.AwayFromZero),
            Application = application,
        };
    }
}
