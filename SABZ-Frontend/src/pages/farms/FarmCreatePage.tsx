import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { farmApi } from '@/api/farmApi';
import { Card } from '@/components/ui/Card';
import { FarmForm } from '@/components/forms/FarmForm';
import { ArrowLeft } from 'lucide-react';
import { t } from '@/lib/i18n';
import type { CreateFarmDto } from '@/types';

export function FarmCreatePage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (data: CreateFarmDto) => {
    setLoading(true);
    try {
      const farm = await farmApi.create(data);
      navigate(`/farms/${farm.id}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto animate-fade-in">
      <button
        onClick={() => navigate(-1)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 mb-6 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('common.back')}
      </button>

      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">{t('farm.add')}</h1>
        <p className="text-gray-500 text-sm mt-1">Fill in the details below to create a new farm</p>
      </div>

      <Card>
        <FarmForm onSubmit={handleSubmit} loading={loading} submitLabel={t('farm.add')} />
      </Card>
    </div>
  );
}
