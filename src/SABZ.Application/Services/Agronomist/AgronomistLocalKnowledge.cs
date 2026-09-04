using SABZ.Application.Services.CropKnowledge;

namespace SABZ.Application.Services.Agronomist;

/// <summary>
/// Local keyword-matching fallback for the AI agronomist (offline mode).
///
/// When the AI provider is not configured, times out or is otherwise
/// unavailable, the assistant tries to answer from the embedded crop
/// knowledge base (Data/crop_knowledge_base.json) by:
///   1. Detecting which of the 8 catalogue crops the question mentions
///      (English, Urdu or scientific name).
///   2. Detecting the topic (fertilizer, watering, disease/pest, sowing
///      time, harvest/maturity) from keywords in English and Urdu.
///   3. Rendering a short, factual answer from the knowledge-base data.
///
/// Returns null when no catalogue crop is mentioned - a fallback answer is
/// never fabricated. Answers are plain English and always marked with the
/// OfflineAnswer limitation by the calling service.
/// </summary>
public static class AgronomistLocalKnowledge
{
    private static readonly string[] FertilizerKeywords =
    {
        "fertilizer", "fertiliser", "fertilizers", "dap", "urea", "ssp", "nutrient",
        "khad", "کھاد"
    };

    private static readonly string[] WaterKeywords =
    {
        "water", "watering", "irrigation", "irrigate", "irrigating", "canal", "tube well",
        "pani", "پانی", "سنچائی"
    };

    private static readonly string[] DiseaseKeywords =
    {
        "disease", "pest", "fungus", "fungal", "insect", "attack", "blight", "rust",
        "mildew", "rot", "sick", "spray", "pesticide", "infection",
        "بیماری", "کیڑ", "کیڑے", "سپرے"
    };

    private static readonly string[] SowingKeywords =
    {
        "sow", "sowing", "plant", "planting", "when to plant", "when should i plant",
        "season to plant", "seed rate",
        "بوائی", "کیا"
    };

    private static readonly string[] HarvestKeywords =
    {
        "harvest", "harvesting", "mature", "maturity", "ready to harvest", "how long",
        "how many days", "cut", "کٹائی"
    };

    /// <summary>
    /// Try to answer the question from the local knowledge base.
    /// Returns null when no catalogue crop is mentioned.
    /// </summary>
    public static string? TryAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var entry = FindMentionedCrop(question);
        if (entry is null)
            return null;

        var lower = question.ToLowerInvariant();
        var topic = DetectTopic(lower);

        return topic switch
        {
            Topic.Fertilizer => BuildFertilizerAnswer(entry),
            Topic.Water => BuildWaterAnswer(entry),
            Topic.Disease => BuildDiseaseAnswer(entry),
            Topic.Sowing => BuildSowingAnswer(entry),
            Topic.Harvest => BuildHarvestAnswer(entry),
            _ => BuildGeneralAnswer(entry),
        };
    }

    /// <summary>
    /// Answer for offline mode when the question mentions no catalogue crop:
    /// explains which crops and topics the built-in knowledge base covers.
    /// Factual capability description only - never fabricated agronomy.
    /// </summary>
    public static string OfflineCapabilitiesAnswer()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("The AI assistant is in offline mode right now. Offline I can answer questions about these crops: ");
        sb.Append(string.Join(", ", CropKnowledgeBase.Entries.Select(e => e.Name)));
        sb.AppendLine(".");
        sb.Append("Ask about a crop with its topic, for example: fertilizer schedule for Wheat, how often to water Rice, " +
                  "diseases of Cotton, or when to harvest Maize. Full AI answers for any question return once the AI service is configured on the server.");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    //  Crop detection
    // ------------------------------------------------------------------

    /// <summary>First knowledge-base crop mentioned in the question (any name form).</summary>
    private static CropKnowledgeEntry? FindMentionedCrop(string question)
    {
        // Longest names first so "Mung Bean" wins over a partial match.
        foreach (var entry in CropKnowledgeBase.Entries
                     .OrderByDescending(e => e.Name.Length))
        {
            if (Mentions(question, entry.Name)
                || (!string.IsNullOrEmpty(entry.NameUrdu) && question.Contains(entry.NameUrdu, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(entry.ScientificName) && question.Contains(entry.ScientificName, StringComparison.OrdinalIgnoreCase)))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Whole-word, case-insensitive containment check.</summary>
    private static bool Mentions(string text, string name)
    {
        var index = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetter(text[index - 1]);
            var after = index + name.Length;
            var afterOk = after >= text.Length || !char.IsLetter(text[after]);
            if (beforeOk && afterOk)
                return true;
            index = text.IndexOf(name, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    // ------------------------------------------------------------------
    //  Topic detection
    // ------------------------------------------------------------------

    private enum Topic { None, Fertilizer, Water, Disease, Sowing, Harvest }

    private static Topic DetectTopic(string lowerQuestion)
    {
        if (MatchesAny(lowerQuestion, FertilizerKeywords)) return Topic.Fertilizer;
        if (MatchesAny(lowerQuestion, WaterKeywords)) return Topic.Water;
        if (MatchesAny(lowerQuestion, DiseaseKeywords)) return Topic.Disease;
        if (MatchesAny(lowerQuestion, SowingKeywords)) return Topic.Sowing;
        if (MatchesAny(lowerQuestion, HarvestKeywords)) return Topic.Harvest;
        return Topic.None;
    }

    private static bool MatchesAny(string lowerQuestion, string[] keywords)
        => keywords.Any(k => lowerQuestion.Contains(k, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------
    //  Answer builders (factual, knowledge-base data only)
    // ------------------------------------------------------------------

    private static string BuildFertilizerAnswer(CropKnowledgeEntry entry)
    {
        var sowing = entry.FertilizerPlan.Sowing;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Standard fertilizer schedule for {entry.Name} ({entry.NameUrdu}) per acre:");
        sb.AppendLine($"- At sowing: {Bags(sowing.Dap)} of DAP" +
            (sowing.Ssp > 0 ? $" and {Bags(sowing.Ssp)} of SSP" : "") +
            (sowing.Urea > 0 ? $" plus {Bags(sowing.Urea)} of Urea" : "") + ".");
        sb.AppendLine($"- 1st irrigation: {Bags(entry.FertilizerPlan.FirstIrrigation.Urea)} of Urea.");
        sb.AppendLine($"- 2nd irrigation: {Bags(entry.FertilizerPlan.SecondIrrigation.Urea)} of Urea.");
        sb.Append("All bags are 50 kg. Apply fertilizer when the soil is moist, and verify with a soil test where possible.");
        return sb.ToString();
    }

    private static string BuildWaterAnswer(CropKnowledgeEntry entry)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{entry.Name} ({entry.NameUrdu}) has a {entry.WaterRequirement.ToLowerInvariant()} water requirement.");
        if (string.Equals(entry.WaterRequirement, "High", StringComparison.OrdinalIgnoreCase))
            sb.Append("Do not let the field dry out for long - irrigate at regular, close intervals and avoid missing a cycle.");
        else if (string.Equals(entry.WaterRequirement, "Low", StringComparison.OrdinalIgnoreCase))
            sb.Append("It tolerates drier conditions - irrigate only when needed and avoid water-logging.");
        else
            sb.Append("Irrigate at moderate intervals; check the soil 2-3 inches down and water when it starts to dry.");
        sb.Append(" Check the SABZ weather alerts before irrigating - rain expected in the next days means you can delay watering.");
        return sb.ToString();
    }

    private static string BuildDiseaseAnswer(CropKnowledgeEntry entry)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Common diseases and pests in {entry.Name} ({entry.NameUrdu}): {string.Join(", ", entry.CommonDiseases)}.");
        sb.Append("Inspect the undersides of leaves weekly for spots, yellowing or insects. " +
            "For a reliable diagnosis, take a clear close-up photo of the affected leaf and use the SABZ Disease Camera.");
        return sb.ToString();
    }

    private static string BuildSowingAnswer(CropKnowledgeEntry entry)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"The sowing window for {entry.Name} ({entry.NameUrdu}) is {MonthName(entry.SowingWindow.StartMonth)} to {MonthName(entry.SowingWindow.EndMonth)} ({entry.Season} season).");
        sb.Append($"It grows best in {string.Join(", ", entry.SuitableSoil)} soil and takes about {entry.MaturityDays} days to mature.");
        return sb.ToString();
    }

    private static string BuildHarvestAnswer(CropKnowledgeEntry entry)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{entry.Name} ({entry.NameUrdu}) typically matures in about {entry.MaturityDays} days after sowing.");
        sb.AppendLine($"Growth stages: germination (days {entry.StageTimeline.Germination.StartDay}-{entry.StageTimeline.Germination.EndDay}), " +
            $"vegetative (days {entry.StageTimeline.Vegetative.StartDay}-{entry.StageTimeline.Vegetative.EndDay}), " +
            $"flowering (days {entry.StageTimeline.Flowering.StartDay}-{entry.StageTimeline.Flowering.EndDay}), " +
            $"maturity (days {entry.StageTimeline.Maturity.StartDay}-{entry.StageTimeline.Maturity.EndDay}).");
        sb.Append("Harvest when the crop reaches the maturity stage - the SABZ crop card shows your crop's current stage progress.");
        return sb.ToString();
    }

    private static string BuildGeneralAnswer(CropKnowledgeEntry entry)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{entry.Name} ({entry.NameUrdu}) quick facts:");
        sb.AppendLine($"- Season: {entry.Season}; sowing window: {MonthName(entry.SowingWindow.StartMonth)} to {MonthName(entry.SowingWindow.EndMonth)}.");
        sb.AppendLine($"- Matures in about {entry.MaturityDays} days; water requirement: {entry.WaterRequirement}.");
        sb.AppendLine($"- Suitable soil: {string.Join(", ", entry.SuitableSoil)}.");
        sb.Append("Ask about fertilizer schedule, watering, diseases, sowing time or harvest for more detail.");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static string Bags(decimal bagsPerAcre)
    {
        var text = bagsPerAcre.ToString("0.##");
        return $"{text} bag{(bagsPerAcre == 1 ? "" : "s")}";
    }

    private static string MonthName(int month)
        => month is >= 1 and <= 12
            ? new DateTime(2026, month, 1).ToString("MMMM")
            : month.ToString();
}
