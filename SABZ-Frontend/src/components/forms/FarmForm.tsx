import { useState, useEffect, type FormEvent } from 'react';
import { locationApi } from '@/api/locationApi';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Alert } from '@/components/ui/Alert';
import { parseApiError } from '@/api/client';
import { t } from '@/lib/i18n';
import { MapPin } from 'lucide-react';
import type { LocationDto, CreateFarmDto } from '@/types';

interface FarmFormProps {
  initial?: CreateFarmDto;
  onSubmit: (data: CreateFarmDto) => Promise<void>;
  loading: boolean;
  submitLabel: string;
  /** Notifies the parent when a tehsil is picked (dashboard onboarding weather preview). */
  onTehsilChange?: (tehsil: { id: number; name: string }) => void;
}

const SOIL_TYPES = [
  'Clay', 'Sandy', 'Loamy', 'Silty', 'Peaty', 'Chalky', 'Saline', 'Alluvial',
];
const IRRIGATION_TYPES = [
  'Canal', 'Tubewell', 'Drip', 'Sprinkler', 'Rain-fed', 'Flood', 'Furrow',
];

export function FarmForm({ initial, onSubmit, loading, submitLabel, onTehsilChange }: FarmFormProps) {
  const [form, setForm] = useState<CreateFarmDto>({
    farmName: initial?.farmName || '',
    provinceId: initial?.provinceId || null,
    districtId: initial?.districtId || null,
    tehsilId: initial?.tehsilId || null,
    latitude: initial?.latitude || null,
    longitude: initial?.longitude || null,
    farmSize: initial?.farmSize || 0,
    farmSizeUnit: initial?.farmSizeUnit || 'Acres',
    soilType: initial?.soilType || '',
    irrigationType: initial?.irrigationType || '',
  });

  const [provinces, setProvinces] = useState<LocationDto[]>([]);
  const [districts, setDistricts] = useState<LocationDto[]>([]);
  const [tehsils, setTehsils] = useState<LocationDto[]>([]);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);

  // Load provinces on mount
  useEffect(() => {
    locationApi.getProvinces().then(setProvinces).catch(() => {});
  }, []);

  // Load districts when province changes
  useEffect(() => {
    if (form.provinceId) {
      setDistricts([]);
      setTehsils([]);
      if (!initial?.districtId) {
        setForm((f) => ({ ...f, districtId: null, tehsilId: null }));
      }
      locationApi
        .getDistricts(form.provinceId)
        .then(setDistricts)
        .catch(() => {});
    }
  }, [form.provinceId]);

  // Load tehsils when district changes
  useEffect(() => {
    if (form.districtId) {
      setTehsils([]);
      if (!initial?.tehsilId) {
        setForm((f) => ({ ...f, tehsilId: null }));
      }
      locationApi
        .getTehsils(form.districtId)
        .then(setTehsils)
        .catch(() => {});
    }
  }, [form.districtId]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setFieldErrors({});
    try {
      await onSubmit({
        ...form,
        latitude: form.latitude || null,
        longitude: form.longitude || null,
        soilType: form.soilType || null,
        irrigationType: form.irrigationType || null,
      });
    } catch (err) {
      const parsed = parseApiError(err);
      setError(parsed.message);
      if (parsed.fieldErrors) setFieldErrors(parsed.fieldErrors);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {error && <Alert variant="error" dismissible>{error}</Alert>}

      {/* Farm name */}
      <Input
        label={t('farm.name')}
        placeholder={t('farm.namePlaceholder')}
        value={form.farmName}
        onChange={(e) => setForm((f) => ({ ...f, farmName: e.target.value }))}
        required
        error={fieldErrors.FarmName?.[0]}
      />

      {/* Location cascading */}
      <div className="space-y-3">
        <h3 className="text-sm font-semibold text-gray-700 flex items-center gap-1.5">
          <MapPin className="h-4 w-4 text-primary-600" />
          {t('farm.location')}
        </h3>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <Select
            label={t('farm.province')}
            value={form.provinceId?.toString() || ''}
            onChange={(e) =>
              setForm((f) => ({
                ...f,
                provinceId: e.target.value ? Number(e.target.value) : null,
                districtId: null,
                tehsilId: null,
              }))
            }
            placeholder="Select province"
            options={provinces.map((p) => ({ value: p.id, label: p.name }))}
            error={fieldErrors.ProvinceId?.[0]}
          />
          <Select
            label={t('farm.district')}
            value={form.districtId?.toString() || ''}
            onChange={(e) =>
              setForm((f) => ({
                ...f,
                districtId: e.target.value ? Number(e.target.value) : null,
                tehsilId: null,
              }))
            }
            placeholder="Select district"
            disabled={!form.provinceId}
            options={districts.map((d) => ({ value: d.id, label: d.name }))}
            error={fieldErrors.DistrictId?.[0]}
          />
          <Select
            label={t('farm.tehsil')}
            value={form.tehsilId?.toString() || ''}
            onChange={(e) => {
              const id = e.target.value ? Number(e.target.value) : null;
              setForm((f) => ({ ...f, tehsilId: id }));
              if (id != null && onTehsilChange) {
                const tehsil = tehsils.find((x) => x.id === id);
                if (tehsil) onTehsilChange({ id, name: tehsil.name });
              }
            }}
            placeholder="Select tehsil"
            disabled={!form.districtId}
            options={tehsils.map((t) => ({ value: t.id, label: t.name }))}
            error={fieldErrors.TehsilId?.[0]}
          />
        </div>
      </div>

      {/* Farm size */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Input
          label={t('farm.size')}
          type="number"
          step="0.01"
          min="0.01"
          placeholder="e.g. 12.5"
          value={form.farmSize || ''}
          onChange={(e) => setForm((f) => ({ ...f, farmSize: Number(e.target.value) }))}
          required
          error={fieldErrors.FarmSize?.[0]}
        />
        <Select
          label={t('farm.sizeUnit')}
          value={form.farmSizeUnit}
          onChange={(e) => setForm((f) => ({ ...f, farmSizeUnit: e.target.value }))}
          options={[
            { value: 'Acres', label: 'Acres' },
            { value: 'Hectares', label: 'Hectares' },
            { value: 'Kanals', label: 'Kanals' },
            { value: 'Marlas', label: 'Marlas' },
          ]}
        />
      </div>

      {/* Soil & irrigation */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Select
          label={t('farm.soilType')}
          value={form.soilType || ''}
          onChange={(e) => setForm((f) => ({ ...f, soilType: e.target.value || null }))}
          placeholder="Select soil type (optional)"
          options={SOIL_TYPES.map((s) => ({ value: s, label: s }))}
        />
        <Select
          label={t('farm.irrigationType')}
          value={form.irrigationType || ''}
          onChange={(e) => setForm((f) => ({ ...f, irrigationType: e.target.value || null }))}
          placeholder="Select irrigation type (optional)"
          options={IRRIGATION_TYPES.map((s) => ({ value: s, label: s }))}
        />
      </div>

      <Button type="submit" loading={loading} size="lg" className="w-full sm:w-auto">
        {submitLabel}
      </Button>
    </form>
  );
}
