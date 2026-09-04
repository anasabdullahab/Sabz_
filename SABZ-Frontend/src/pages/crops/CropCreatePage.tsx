import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { cropApi } from '@/api/cropApi';
import { Card } from '@/components/ui/Card';
import { CropForm } from '@/components/forms/CropForm';
import { ArrowLeft } from 'lucide-react';
import { t } from '@/lib/i18n';
import type { CreateCropDto } from '@/types';

export function CropCreatePage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (data: CreateCropDto) => {
    if (!farmId) return;
    setLoading(true);
    try {
      await cropApi.create(farmId, data);
      navigate(`/farms/${farmId}/crops`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto animate-fade-in">
      <button onClick={() => navigate(`/farms/${farmId}/crops`)} className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 mb-6 transition-colors">
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">{t('crop.add')}</h1>
        <p className="text-gray-500 text-sm mt-1">Add a new crop record to this farm</p>
      </div>
      <Card>
        <CropForm onSubmit={handleSubmit} loading={loading} submitLabel={t('crop.add')} />
      </Card>
    </div>
  );
}
