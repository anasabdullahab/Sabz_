import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Alert } from '@/components/ui/Alert';
import { parseApiError } from '@/api/client';
import { cropKnowledgeApi } from '@/api/cropKnowledgeApi';
import { findKnowledgeEntry, estimateHarvestDate } from '@/lib/cropStages';
import { formatDate } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { CalendarClock } from 'lucide-react';
import { CropPicker } from '@/components/forms/CropPicker';
import type { CreateCropDto, CropKnowledgeEntryDto } from '@/types';

interface CropFormProps {
  initial?: CreateCropDto;
  onSubmit: (data: CreateCropDto) => Promise<void>;
  loading: boolean;
  submitLabel: string;
}

const SEASONS = ['Rabi', 'Kharif', 'Other'];
const STATUSES = ['Active', 'Planned', 'Harvested', 'Failed'];
const GROWTH_STAGES = ['Germination', 'Vegetative', 'Flowering', 'Fruiting', 'Maturity', 'Harvested'];

/**
 * Crop form (overhauled):
 *  - Crop name is a searchable dropdown over the local knowledge base with a
 *    free-text fallback for custom crops.
 *  - No harvest date input: the harvest window is estimated automatically from
 *    the planting date + knowledge-base maturity days.
 *  - Season auto-fills from the knowledge base when a known crop is picked.
 */
export function CropForm({ initial, onSubmit, loading, submitLabel }: CropFormProps) {
  const [form, setForm] = useState<CreateCropDto>({
    cropName: initial?.cropName || '',
    cropCatalogId: initial?.cropCatalogId || null,
    season: initial?.season || '',
    plantingDate: initial?.plantingDate || '',
    harvestDate: initial?.harvestDate || '',
    growthStage: initial?.growthStage || '',
    previousCrop: initial?.previousCrop || '',
    status: initial?.status || 'Active',
  });
  const [entries, setEntries] = useState<CropKnowledgeEntryDto[]>([]);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    cropKnowledgeApi.getCrops().then(setEntries).catch(() => { /* custom crops still work */ });
  }, []);

  const selectedEntry = useMemo(
    () => findKnowledgeEntry(form.cropName, entries),
    [form.cropName, entries],
  );

  const harvestEstimate = estimateHarvestDate(selectedEntry, form.plantingDate);

  const handleCropChange = (name: string, entry: CropKnowledgeEntryDto | null) => {
    setForm((f) => ({
      ...f,
      cropName: name,
      // Auto-fill the season from the knowledge base for known crops
      season: entry ? entry.season : f.season,
    }));
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setFieldErrors({});
    try {
      await onSubmit({
        ...form,
        plantingDate: form.plantingDate || null,
        // Harvest window is auto-calculated; keep any existing date for
        // custom crops the knowledge base cannot estimate.
        harvestDate: harvestEstimate
          ? harvestEstimate.toISOString()
          : (initial?.harvestDate ?? null),
        growthStage: form.growthStage || null,
        previousCrop: form.previousCrop || null,
        status: form.status || null,
      });
    } catch (err) {
      const parsed = parseApiError(err);
      setError(parsed.message);
      if (parsed.fieldErrors) setFieldErrors(parsed.fieldErrors);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      {error && <Alert variant="error" dismissible>{error}</Alert>}

      {/* Crop name: searchable dropdown with custom fallback */}
      <CropPicker
        value={form.cropName}
        entries={entries}
        onChange={handleCropChange}
        error={fieldErrors.CropName?.[0]}
      />

      <Select
        label={t('crop.season')}
        value={form.season}
        onChange={(e) => setForm((f) => ({ ...f, season: e.target.value }))}
        placeholder="Select season"
        required
        options={SEASONS.map((s) => ({ value: s, label: s }))}
        error={fieldErrors.Season?.[0]}
      />

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Input
          label={t('crop.plantingDate')}
          type="date"
          value={form.plantingDate?.split('T')[0] || ''}
          onChange={(e) => setForm((f) => ({ ...f, plantingDate: e.target.value || null }))}
        />
        <div className="flex items-end">
          {/* Estimated harvest replaces the manual harvest date input */}
          {selectedEntry ? (
            <div className="w-full rounded-xl bg-primary-50/70 border border-primary-100 px-3.5 py-2.5 flex items-start gap-2.5">
              <CalendarClock className="h-4 w-4 text-primary-600 mt-0.5 shrink-0" />
              <div className="text-xs">
                <p className="font-semibold text-primary-800">
                  {t('crop.estimatedHarvest')}:{' '}
                  {harvestEstimate ? `~ ${formatDate(harvestEstimate.toISOString())}` : '—'}
                </p>
                <p className="text-gray-500 mt-0.5">{t('crop.harvestAutoNote')}</p>
              </div>
            </div>
          ) : (
            <p className="text-[11px] text-gray-400 leading-snug">{t('crop.harvestHintCustom')}</p>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Select
          label={t('crop.growthStage')}
          value={form.growthStage || ''}
          onChange={(e) => setForm((f) => ({ ...f, growthStage: e.target.value || null }))}
          placeholder="Select stage (optional)"
          options={GROWTH_STAGES.map((s) => ({ value: s, label: s }))}
        />
        <Select
          label={t('crop.status')}
          value={form.status || ''}
          onChange={(e) => setForm((f) => ({ ...f, status: e.target.value || null }))}
          placeholder="Select status (optional)"
          options={STATUSES.map((s) => ({ value: s, label: s }))}
        />
      </div>

      <Input
        label={t('crop.previousCrop')}
        placeholder="e.g. Wheat (optional)"
        value={form.previousCrop || ''}
        onChange={(e) => setForm((f) => ({ ...f, previousCrop: e.target.value || null }))}
      />

      <Button type="submit" loading={loading} size="lg" className="w-full sm:w-auto">
        {submitLabel}
      </Button>
    </form>
  );
}
