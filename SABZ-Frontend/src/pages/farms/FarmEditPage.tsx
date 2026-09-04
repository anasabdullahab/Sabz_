import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { farmApi } from '@/api/farmApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { FarmForm } from '@/components/forms/FarmForm';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/EmptyState';
import { ArrowLeft } from 'lucide-react';
import { t } from '@/lib/i18n';
import type { CreateFarmDto, FarmResponseDto } from '@/types';

export function FarmEditPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();
  const [farm, setFarm] = useState<FarmResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!farmId) return;
    farmApi.getById(farmId).then(setFarm).catch((err) => setError(parseApiError(err).message)).finally(() => setLoading(false));
  }, [farmId]);

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={() => farmId && farmApi.getById(farmId).then(setFarm)} />;
  if (!farm) return <ErrorState message={t('farm.notFound')} />;

  const initial: CreateFarmDto = {
    farmName: farm.farmName,
    provinceId: farm.provinceId,
    districtId: farm.districtId,
    tehsilId: farm.tehsilId,
    latitude: farm.latitude,
    longitude: farm.longitude,
    farmSize: farm.farmSize,
    farmSizeUnit: farm.farmSizeUnit,
    soilType: farm.soilType,
    irrigationType: farm.irrigationType,
  };

  const handleSubmit = async (data: CreateFarmDto) => {
    setSaving(true);
    try {
      const updated = await farmApi.update(farm.id, data);
      navigate(`/farms/${updated.id}`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto animate-fade-in">
      <button
        onClick={() => navigate(`/farms/${farm.id}`)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 mb-6 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('common.back')}
      </button>

      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">{t('farm.edit')}</h1>
        <p className="text-gray-500 text-sm mt-1">Update the details for {farm.farmName}</p>
      </div>

      <Card>
        <FarmForm
          initial={initial}
          onSubmit={handleSubmit}
          loading={saving}
          submitLabel={t('common.save')}
        />
      </Card>
    </div>
  );
}
