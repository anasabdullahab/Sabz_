import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { cropApi } from '@/api/cropApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { CropForm } from '@/components/forms/CropForm';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/EmptyState';
import { ArrowLeft } from 'lucide-react';
import { t } from '@/lib/i18n';
import type { CreateCropDto, CropResponseDto } from '@/types';

export function CropEditPage() {
  const { cropId, farmId } = useParams<{ cropId: string; farmId: string }>();
  const navigate = useNavigate();
  const [crop, setCrop] = useState<CropResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!cropId) return;
    cropApi.getById(cropId).then(setCrop).catch((err) => setError(parseApiError(err).message)).finally(() => setLoading(false));
  }, [cropId]);

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} />;
  if (!crop) return <ErrorState message="Crop not found." />;

  const initial: CreateCropDto = {
    cropName: crop.cropName,
    cropCatalogId: crop.cropCatalogId,
    season: crop.season,
    plantingDate: crop.plantingDate,
    harvestDate: crop.harvestDate,
    growthStage: crop.growthStage,
    previousCrop: crop.previousCrop,
    status: crop.status,
  };

  const handleSubmit = async (data: CreateCropDto) => {
    setSaving(true);
    try {
      await cropApi.update(crop.id, data);
      navigate(`/farms/${farmId}/crops`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto animate-fade-in">
      <button onClick={() => navigate(`/farms/${farmId}/crops`)} className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 mb-6 transition-colors">
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">{t('crop.edit')}</h1>
        <p className="text-gray-500 text-sm mt-1">Update the details for {crop.cropName}</p>
      </div>
      <Card>
        <CropForm initial={initial} onSubmit={handleSubmit} loading={saving} submitLabel={t('common.save')} />
      </Card>
    </div>
  );
}
