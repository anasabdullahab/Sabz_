import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { farmApi } from '@/api/farmApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { t } from '@/lib/i18n';
import { MapPin, Sprout, Layers, ArrowRight } from 'lucide-react';
import type { FarmResponseDto } from '@/types';

/**
 * Farm picker for farm-scoped utility tools (Smart Recommendations,
 * Disease Camera). Keeps the sidebar simple: the tool links here and the
 * farmer picks which farm to open the tool for.
 *
 * UX rules:
 *  - exactly one farm → skip the picker and open the tool directly
 *  - no farms yet → offer the Add Farm wizard instead of a dead end
 */
export function SelectFarmPage({ pathTemplate, icon: Icon }: { pathTemplate: string; icon: React.ElementType }) {
  const navigate = useNavigate();
  const [farms, setFarms] = useState<FarmResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    farmApi.getAll()
      .then((f) => {
        setFarms(f);
        // Single farm: jump straight into the tool (replace so Back skips the picker)
        if (f.length === 1) {
          navigate(pathTemplate.replace(':farmId', f[0].id), { replace: true });
        }
      })
      .catch((err) => setError(parseApiError(err).message))
      .finally(() => setLoading(false));
  }, [navigate, pathTemplate]);

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={() => window.location.reload()} />;

  return (
    <div className="space-y-6 animate-fade-in max-w-3xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center">
            <Icon className="h-5 w-5 text-white" />
          </div>
          {t('nav.selectFarm')}
        </h1>
        <p className="text-gray-500 text-sm mt-1 ml-[52px]">{t('nav.selectFarmDescription')}</p>
      </div>

      {farms.length === 0 ? (
        <EmptyState
          icon={<Sprout className="h-16 w-16" />}
          title={t('nav.selectFarmNoFarms')}
          description={t('nav.selectFarmDescription')}
          action={{ label: t('dashboard.addFirstFarm'), onClick: () => navigate('/farms/new') }}
        />
      ) : (
        <div className="space-y-3">
          {farms.map((farm) => (
            <Card
              key={farm.id}
              padding="sm"
              hover
              onClick={() => navigate(pathTemplate.replace(':farmId', farm.id))}
            >
              <div className="flex items-center gap-4">
                <div className="h-11 w-11 rounded-xl bg-primary-50 flex items-center justify-center shrink-0">
                  <Sprout className="h-5 w-5 text-primary-700" />
                </div>
                <div className="flex-1 min-w-0">
                  <h3 className="font-semibold text-gray-900 truncate">{farm.farmName}</h3>
                  <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-gray-500 mt-0.5">
                    <span className="flex items-center gap-1">
                      <MapPin className="h-3 w-3" />
                      {farm.tehsilName}, {farm.districtName}
                    </span>
                    <span className="flex items-center gap-1">
                      <Layers className="h-3 w-3" />
                      {farm.farmSize} {farm.farmSizeUnit}
                    </span>
                  </div>
                </div>
                <ArrowRight className="h-5 w-5 text-gray-300 shrink-0" />
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
