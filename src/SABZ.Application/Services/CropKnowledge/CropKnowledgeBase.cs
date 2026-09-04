using System.Reflection;
using System.Text.Json;

namespace SABZ.Application.Services.CropKnowledge;

/// <summary>
/// In-memory representation of the local crop knowledge base
/// (Data/crop_knowledge_base.json, embedded in the assembly).
/// 100% local data - no external calls, no API keys.
/// </summary>
public sealed class CropKnowledgeEntry
{
    public string Name { get; set; } = string.Empty;
    public string NameUrdu { get; set; } = string.Empty;
    public string ScientificName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public int MaturityDays { get; set; }
    public SowingWindow SowingWindow { get; set; } = new();
    public List<string> SuitableSoil { get; set; } = new();
    public string NitrogenImpact { get; set; } = string.Empty;
    public StageTimeline StageTimeline { get; set; } = new();
    public string WaterRequirement { get; set; } = string.Empty;
    public TemperatureRange TemperatureRange { get; set; } = new();
    public FertilizerPlan FertilizerPlan { get; set; } = new();
    public List<string> CommonDiseases { get; set; } = new();
}

public sealed class SowingWindow
{
    public int StartMonth { get; set; }
    public int EndMonth { get; set; }
}

public sealed class StageTimeline
{
    public StageRange Germination { get; set; } = new();
    public StageRange Vegetative { get; set; } = new();
    public StageRange Flowering { get; set; } = new();
    public StageRange Maturity { get; set; } = new();
}

public sealed class StageRange
{
    public int StartDay { get; set; }
    public int EndDay { get; set; }
}

public sealed class TemperatureRange
{
    public decimal MinC { get; set; }
    public decimal MaxC { get; set; }
}

public sealed class FertilizerPlan
{
    public FertilizerApplication Sowing { get; set; } = new();
    public FertilizerApplication FirstIrrigation { get; set; } = new();
    public FertilizerApplication SecondIrrigation { get; set; } = new();
}

public sealed class FertilizerApplication
{
    public decimal Dap { get; set; }
    public decimal Ssp { get; set; }
    public decimal Urea { get; set; }
}

/// <summary>
/// Lazy-loaded singleton provider for the crop knowledge base.
/// Reads the embedded JSON once and serves entries from memory.
/// </summary>
public static class CropKnowledgeBase
{
    private static readonly Lazy<List<CropKnowledgeEntry>> _entries = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<CropKnowledgeEntry> Entries => _entries.Value;

    public static CropKnowledgeEntry? Find(string cropName)
    {
        if (string.IsNullOrWhiteSpace(cropName)) return null;
        var trimmed = cropName.Trim();
        return _entries.Value.FirstOrDefault(e =>
            string.Equals(e.Name, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.NameUrdu, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ScientificName, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Standard fertilizer bag weight in kg (all presets use 50 kg bags).</summary>
    public const decimal BagWeightKg = 50m;

    private static List<CropKnowledgeEntry> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("crop_knowledge_base.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("crop_knowledge_base.json embedded resource not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Could not read crop_knowledge_base.json embedded resource.");
        using var reader = new StreamReader(stream);

        var doc = JsonDocument.Parse(reader.ReadToEnd());

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = doc.RootElement.GetProperty("crops").Deserialize<List<CropKnowledgeEntry>>(options)
            ?? new List<CropKnowledgeEntry>();

        return result;
    }
}
