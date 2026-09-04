import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { farmApi } from '@/api/farmApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { t } from '@/lib/i18n';
import { Plus, MapPin, Sprout, Cloud, Target, Lightbulb, Layers, Droplets } from 'lucide-react';
import type { FarmResponseDto } from '@/types';

export function FarmsPage() {
  const navigate = useNavigate();
  const [farms, setFarms] = useState<FarmResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { loadFarms(); }, []);

  const loadFarms = async () => {
    setLoading(true);
    setError(null);
    try {
      setFarms(await farmApi.getAll());
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={loadFarms} />;

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{t('nav.farms')}</h1>
          <p className="text-gray-500 text-sm mt-0.5">Manage all your farms</p>
        </div>
        <Button onClick={() => navigate('/farms/new')}>
          <Plus className="h-4 w-4" />
          {t('farm.add')}
        </Button>
      </div>

      {farms.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Sprout className="h-16 w-16" />}
            title={t('dashboard.noFarms')}
            description="Add your first farm to get started with crop management, weather intelligence, and AI recommendations."
            action={{ label: t('dashboard.addFirstFarm'), onClick: () => navigate('/farms/new') }}
          />
        </Card>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {farms.map((farm) => (
            <Card key={farm.id} hover onClick={() => navigate(`/farms/${farm.id}`)} padding="none">
              <div className="h-1.5 bg-gradient-to-r from-primary-500 to-primary-600 rounded-t-2xl" />
              <div className="p-5">
                <div className="flex items-start justify-between mb-3">
                  <div>
                    <h3 className="font-semibold text-gray-900">{farm.farmName}</h3>
                    <div className="flex items-center gap-1.5 text-xs text-gray-500 mt-0.5">
                      <MapPin className="h-3 w-3" />
                      {[farm.provinceName, farm.districtName].filter(Boolean).join(', ') || t('farm.noLocation')}
                    </div>
                  </div>
                  <span className="text-sm font-semibold text-gray-900">
                    {farm.farmSize} {farm.farmSizeUnit}
                  </span>
                </div>
                <div className="grid grid-cols-2 gap-2 mb-4">
                  {farm.soilType && (
                    <div className="flex items-center gap-1.5 text-xs text-gray-600">
                      <Layers className="h-3.5 w-3.5 text-earth-500" /> {farm.soilType}
                    </div>
                  )}
                  {farm.irrigationType && (
                    <div className="flex items-center gap-1.5 text-xs text-gray-600">
                      <Droplets className="h-3.5 w-3.5 text-sky-500" /> {farm.irrigationType}
                    </div>
                  )}
                </div>
                <div className="flex gap-2 pt-3 border-t border-gray-100">
                  {[
                    { icon: Cloud, label: 'Weather', path: 'weather' },
                    { icon: Sprout, label: 'Crops', path: 'crops' },
                    { icon: Target, label: 'Suitability', path: 'crop-suitability' },
                    { icon: Lightbulb, label: 'Recommend', path: 'crop-recommendations' },
                  ].map(({ icon: Icon, label, path }) => (
                    <button
                      key={path}
                      onClick={(e) => { e.stopPropagation(); navigate(`/farms/${farm.id}/${path}`); }}
                      className="flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg text-xs font-medium text-gray-500 hover:text-primary-700 hover:bg-primary-50 transition-colors"
                    >
                      <Icon className="h-3.5 w-3.5" />
                      {label}
                    </button>
                  ))}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
