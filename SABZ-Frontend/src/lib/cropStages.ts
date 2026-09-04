import type { CropKnowledgeEntryDto } from '@/types';

/**
 * Crop stage helpers built on the local crop knowledge base.
 * Used by the crop form (harvest-window estimation) and the active crop
 * cards (visual stage progress bar).
 */

export interface CropStageInfo {
  key: 'germination' | 'vegetative' | 'flowering' | 'maturity';
  label: string;
  startDay: number;
  endDay: number;
}

export interface CropStageProgress {
  /** Days elapsed since planting. */
  dayNumber: number;
  /** Knowledge-base maturity period in days. */
  maturityDays: number;
  /** Overall growth progress, 0-100. */
  percent: number;
  /** Estimated harvest date (planting + maturityDays). */
  estimatedHarvest: Date;
  currentStage: CropStageInfo | null;
  stages: Array<CropStageInfo & { status: 'done' | 'active' | 'upcoming' }>;
}

const DAY_MS = 86_400_000;

/**
 * Finds the knowledge-base entry for a crop name using variant matching:
 * exact/Urdu match first, then "Gram (Chickpea)" -> "chickpea" / "gram"
 * style variants (mirrors the backend recommendation engine).
 */
export function findKnowledgeEntry(
  cropName: string | null | undefined,
  entries: CropKnowledgeEntryDto[],
): CropKnowledgeEntryDto | null {
  if (!cropName) return null;
  const trimmed = cropName.trim();
  if (!trimmed) return null;

  // 1. Exact match against canonical, Urdu and lowercased names
  const exact = entries.find((e) =>
    e.name.toLowerCase() === trimmed.toLowerCase() ||
    (e.nameUrdu && e.nameUrdu === trimmed),
  );
  if (exact) return exact;

  // 2. Variant match — every variant of the crop name vs every variant of
  //    each knowledge-base crop name.
  const cropVariants = nameVariants(trimmed);
  for (const entry of entries) {
    const kbVariants = nameVariants(entry.name);
    if (cropVariants.some((v) => kbVariants.includes(v))) return entry;
  }
  return null;
}

/** "Gram (Chickpea)" -> ["gram (chickpea)", "chickpea", "gram"] */
function nameVariants(name: string): string[] {
  const variants = [name.trim().toLowerCase()];
  const open = name.indexOf('(');
  if (open >= 0) {
    const close = name.indexOf(')', open);
    if (close > open) {
      const inner = name.substring(open + 1, close).trim().toLowerCase();
      const outer = name.substring(0, open).trim().toLowerCase();
      if (inner) variants.push(inner);
      if (outer) variants.push(outer);
    }
  }
  return variants;
}

/** Ordered stage list derived from a knowledge-base entry. */
export function getCropStages(entry: CropKnowledgeEntryDto): CropStageInfo[] {
  const tl = entry.stageTimeline;
  return [
    { key: 'germination', label: 'Germination', startDay: tl.germination[0], endDay: tl.germination[1] },
    { key: 'vegetative', label: 'Vegetative', startDay: tl.vegetative[0], endDay: tl.vegetative[1] },
    { key: 'flowering', label: 'Flowering', startDay: tl.flowering[0], endDay: tl.flowering[1] },
    { key: 'maturity', label: 'Maturity', startDay: tl.maturity[0], endDay: tl.maturity[1] },
  ];
}

/**
 * Computes the growth-stage progress for a crop with a known knowledge-base
 * entry and planting date. Returns null when progress cannot be determined
 * (custom crop or missing planting date).
 */
export function getStageProgress(
  entry: CropKnowledgeEntryDto | null | undefined,
  plantingDate: string | null | undefined,
  now: Date = new Date(),
): CropStageProgress | null {
  if (!entry || !plantingDate) return null;

  const planted = new Date(plantingDate);
  if (Number.isNaN(planted.getTime())) return null;

  const dayNumber = Math.max(0, Math.floor((now.getTime() - planted.getTime()) / DAY_MS));
  const maturityDays = Math.max(1, entry.maturityDays);
  const percent = Math.min(100, Math.round((dayNumber / maturityDays) * 100));
  const estimatedHarvest = new Date(planted.getTime() + maturityDays * DAY_MS);

  const stages = getCropStages(entry);
  const withStatus = stages.map((s) => ({
    ...s,
    status:
      dayNumber > s.endDay ? ('done' as const)
      : dayNumber >= s.startDay ? ('active' as const)
      : ('upcoming' as const),
  }));

  const active = withStatus.find((s) => s.status === 'active') ?? null;
  const currentStage = active
    ?? (dayNumber > stages[stages.length - 1].endDay ? stages[stages.length - 1] : null);

  return { dayNumber, maturityDays, percent, estimatedHarvest, currentStage, stages: withStatus };
}

/**
 * Estimated harvest window for the crop form: planting date + knowledge-base
 * maturity period. Returns null for custom crops without KB data.
 */
export function estimateHarvestDate(
  entry: CropKnowledgeEntryDto | null | undefined,
  plantingDate: string | null | undefined,
): Date | null {
  if (!entry || !plantingDate) return null;
  const planted = new Date(plantingDate);
  if (Number.isNaN(planted.getTime())) return null;
  return new Date(planted.getTime() + Math.max(1, entry.maturityDays) * DAY_MS);
}
