using SABZ.Application.DTOs.CropPrice;
using SABZ.Application.Interfaces;

namespace SABZ.Infrastructure.Services.CropPrice;

/// <summary>
/// Crop price provider backed by a clearly-labelled SABZ reference dataset
/// (Prompt 17).
///
/// AMIS Punjab (www.amis.pk) was inspected before implementation: it serves
/// ASP.NET pages (ViewPrices.aspx / BrowsePrices.aspx) plus an Android app and
/// does not publish a stable machine-readable endpoint, so no live HTTP
/// provider is wired up yet. This implementation is the honest fallback the
/// specification calls for: a NON-LIVE reference dataset, clearly identified
/// with source "SABZ Reference Dataset" and dataStatus "Reference".
///
/// The dataset reuses the existing 22-crop CropCatalog names and Pakistan's
/// Punjab location hierarchy - it is not a second catalog and not a duplicate
/// location table. Prices are illustrative wholesale reference values
/// (Rs per 100 Kg, matching AMIS's published "Rs/100Kg" convention) over a
/// fixed historical window. Nothing here is persisted and nothing claims to
/// be live. Replace this class with a real AMIS provider in DI when a stable
/// endpoint becomes available - the Application/API layers stay unchanged.
/// </summary>
public class ReferenceCropPriceProvider : ICropPriceProvider
{
    public string SourceName => "SABZ Reference Dataset";

    public bool IsLive => false;

    /// <summary>Fixed historical reference window (non-live by design).</summary>
    private static readonly DateTime ReferenceStart = new(2026, 8, 20);
    private const int ReferenceDays = 5;

    /// <summary>Punjab districts used by the reference dataset.</summary>
    private static readonly string[] Districts =
    {
        "Lahore", "Faisalabad", "Multan", "Rawalpindi", "Gujranwala", "Bahawalpur", "Sahiwal", "Sargodha"
    };

    /// <summary>
    /// One reference crop: canonical CropCatalog name, illustrative base price
    /// (Rs per the source unit) and the subset of districts where it is quoted.
    /// Not all 22 catalog crops are present on purpose - the provider only
    /// returns data it actually has.
    /// </summary>
    private sealed record ReferenceCrop(string Name, decimal BasePrice, string Unit, int[] DistrictIndexes);

    private static readonly ReferenceCrop[] Crops =
    {
        new("Wheat",            4200m,  "100Kg", new[] { 0, 1, 2, 4 }),
        new("Rice",            17000m,  "100Kg", new[] { 1, 2, 5 }),
        new("Maize",            3600m,  "100Kg", new[] { 1, 4, 7 }),
        new("Potato",           3000m,  "100Kg", new[] { 0, 4, 7 }),
        new("Tomato",           8000m,  "100Kg", new[] { 0, 2, 6 }),
        new("Onion",            3500m,  "100Kg", new[] { 2, 5, 6 }),
        new("Chili Pepper",    30000m,  "100Kg", new[] { 2, 5 }),
        new("Mango",           12000m,  "100Kg", new[] { 2, 5, 6 }),
        new("Citrus",           7000m,  "100Kg", new[] { 1, 4, 7 }),
        new("Gram (Chickpea)", 12000m,  "100Kg", new[] { 2, 5, 7 }),
        new("Mustard",         13000m,  "100Kg", new[] { 1, 6 }),
        new("Sugarcane",        3800m,  "100Kg", new[] { 1, 4, 6 }),
        new("Cotton",           8500m,  "100Kg", new[] { 2, 5, 6 }),
        new("Barley",           3200m,  "100Kg", new[] { 2, 7 }),
        new("Sunflower",        9000m,  "100Kg", new[] { 1, 6 }),
        new("Groundnut",       20000m,  "100Kg", new[] { 2, 5 })
    };

    public Task<IReadOnlyList<CropPriceRecordDto>> GetRecordsAsync(CancellationToken ct = default)
    {
        var records = new List<CropPriceRecordDto>();

        foreach (var crop in Crops)
        {
            for (var day = 0; day < ReferenceDays; day++)
            {
                var priceDate = ReferenceStart.AddDays(day);
                foreach (var districtIndex in crop.DistrictIndexes)
                {
                    var district = Districts[districtIndex];
                    records.Add(new CropPriceRecordDto
                    {
                        CropName = crop.Name,
                        Province = "Punjab",
                        District = district,
                        Market = district + " Wholesale Market",
                        // Deterministic illustrative variation - never random, never
                        // presented as live market data.
                        Price = crop.BasePrice + (day * 25m) + (districtIndex * 15m),
                        Unit = crop.Unit,
                        PriceDate = priceDate,
                        Source = SourceName,
                        DataStatus = CropPriceDataStatuses.Reference
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<CropPriceRecordDto>>(records);
    }
}
