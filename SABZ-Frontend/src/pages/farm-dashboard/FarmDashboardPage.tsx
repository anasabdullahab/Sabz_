import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { farmDashboardApi } from '@/api/farmDashboardApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { WeatherAlertsCard } from '@/components/weather/WeatherAlertsCard';
import { formatDate, cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  ArrowLeft, MapPin, Sprout, ClipboardCheck, Bell, DollarSign,
  TrendingUp, Cloud, Maximize, Layers, Droplets, Info,
} from 'lucide-react';
import type { FarmDashboardDto } from '@/types';

export function FarmDashboardPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<FarmDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    if (!farmId) return;
    setLoading(true);
    try {
      const d = await farmDashboardApi.getDashboard(farmId);
      setData(d);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [farmId]);

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={load} />;
  if (!data) return null;

  const fmt = (n: number) =>
    `PKR ${Math.abs(n).toLocaleString('en-PK', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

  return (
    <div className="space-y-6 animate-fade-in">
      <button
        onClick={() => navigate(`/farms/${farmId}`)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      {/* Farm header */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="h-2 bg-gradient-to-r from-primary-500 via-emerald-500 to-teal-500" />
        <div className="p-6 lg:p-8">
          <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 mb-2">{data.farm.farmName}</h1>
          <div className="flex flex-wrap items-center gap-4 text-sm text-gray-500">
            <span className="flex items-center gap-1.5">
              <MapPin className="h-4 w-4" />
              {[data.farm.tehsil, data.farm.district, data.farm.province].filter(Boolean).join(', ')}
            </span>
            <span className="flex items-center gap-1.5">
              <Maximize className="h-4 w-4" />
              {data.farm.farmSize} {data.farm.farmSizeUnit}
            </span>
            {data.farm.soilType && (
              <span className="flex items-center gap-1.5"><Layers className="h-4 w-4" />{data.farm.soilType}</span>
            )}
            {data.farm.irrigationType && (
              <span className="flex items-center gap-1.5"><Droplets className="h-4 w-4" />{data.farm.irrigationType}</span>
            )}
          </div>
        </div>
      </div>

      {/* Smart weather action alerts (compact — hidden when all clear) */}
      <WeatherAlertsCard farmId={farmId!} limit={2} hideWhenEmpty />

      {/* Quick stats row */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard icon={Sprout} label={t('farmDashboard.cropsSection')} value={`${data.crops.activeCrops} active`} color="primary" />
        <StatCard icon={ClipboardCheck} label={t('farmDashboard.monitoringSection')} value={`${data.monitoring.dueChecks} due`} color="amber" />
        <StatCard
          icon={DollarSign}
          label={t('farmDashboard.financialSection')}
          value={fmt(data.financial.netResult)}
          color={data.financial.netResult >= 0 ? 'emerald' : 'rose'}
        />
        <StatCard
          icon={Bell}
          label={t('farmDashboard.alertsSection')}
          value={String(data.notifications.unreadCount)}
          color={data.notifications.unreadCount > 0 ? 'rose' : 'gray'}
        />
      </div>

      {/* Weather (if available) */}
      {data.weather?.current && (
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-3">
            <Cloud className="h-4 w-4 text-sky-500" />
            <h3 className="text-sm font-semibold text-gray-900">{t('farmDashboard.weatherSection')}</h3>
            <Badge variant="info" size="sm">{data.weather.source}</Badge>
          </div>
          <div className="flex items-center gap-6">
            {data.weather.current.temperature != null && (
              <div>
                <p className="text-3xl font-bold text-gray-900">{Math.round(data.weather.current.temperature)}°C</p>
                <p className="text-xs text-gray-500">{t('weather.current')}</p>
              </div>
            )}
            {data.weather.current.relativeHumidity != null && (
              <div>
                <p className="text-sm font-semibold text-gray-700">{data.weather.current.relativeHumidity}%</p>
                <p className="text-xs text-gray-500">{t('weather.humidity')}</p>
              </div>
            )}
            {data.weather.current.windSpeed != null && (
              <div>
                <p className="text-sm font-semibold text-gray-700">{data.weather.current.windSpeed} km/h</p>
                <p className="text-xs text-gray-500">{t('weather.wind')}</p>
              </div>
            )}
          </div>
          <p className="text-[10px] text-gray-400 mt-2">{data.weather.note}</p>
        </Card>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Crops */}
        <Card padding="sm">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <Sprout className="h-4 w-4 text-primary-600" />
              <h3 className="text-sm font-semibold text-gray-900">{t('farmDashboard.cropsSection')}</h3>
            </div>
            <Badge variant="primary" size="sm">{data.crops.totalCrops}</Badge>
          </div>
          {data.crops.crops.length === 0 ? (
            <p className="text-xs text-gray-400 text-center py-4">{t('crop.noCrops')}</p>
          ) : (
            <div className="space-y-2">
              {data.crops.crops.slice(0, 5).map((crop) => (
                <div key={crop.cropId} className="flex items-center justify-between py-1">
                  <div>
                    <p className="text-sm font-medium text-gray-900">{crop.cropName}</p>
                    <p className="text-xs text-gray-500">{crop.season}{crop.growthStage ? ` · ${crop.growthStage}` : ''}</p>
                  </div>
                  <Badge variant={crop.status?.toLowerCase() === 'active' ? 'success' : 'neutral'} size="sm">
                    {crop.status}
                  </Badge>
                </div>
              ))}
            </div>
          )}
        </Card>

        {/* Monitoring */}
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-3">
            <ClipboardCheck className="h-4 w-4 text-amber-600" />
            <h3 className="text-sm font-semibold text-gray-900">{t('farmDashboard.monitoringSection')}</h3>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <MiniStat label={t('farmDashboard.dueChecks')} value={data.monitoring.dueChecks} color="text-amber-600" />
            <MiniStat label={t('farmDashboard.upcomingChecks')} value={data.monitoring.upcomingChecks} color="text-sky-600" />
            <MiniStat label={t('farmDashboard.completedChecks')} value={data.monitoring.completedChecks} color="text-emerald-600" />
            <MiniStat label={t('monitoring.skipped')} value={data.monitoring.skippedChecks} color="text-gray-400" />
          </div>
        </Card>

        {/* Financial */}
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-3">
            <DollarSign className="h-4 w-4 text-emerald-600" />
            <h3 className="text-sm font-semibold text-gray-900">{t('farmDashboard.financialSection')}</h3>
          </div>
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">{t('financial.income')}</span>
              <span className="font-semibold text-emerald-600">+{fmt(data.financial.totalIncome)}</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">{t('financial.expense')}</span>
              <span className="font-semibold text-rose-600">-{fmt(data.financial.totalExpenses)}</span>
            </div>
            <div className="flex justify-between text-sm pt-2 border-t border-gray-100">
              <span className="font-medium text-gray-900">{t('performance.netResult')}</span>
              <span className={cn('font-bold', data.financial.netResult >= 0 ? 'text-emerald-600' : 'text-rose-600')}>
                {data.financial.netResult >= 0 ? '+' : '-'}{fmt(data.financial.netResult)}
              </span>
            </div>
            <p className="text-xs text-gray-400">{data.financial.transactionCount} transactions</p>
          </div>
        </Card>

        {/* Performance */}
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-3">
            <TrendingUp className="h-4 w-4 text-violet-600" />
            <h3 className="text-sm font-semibold text-gray-900">{t('farmDashboard.performanceSection')}</h3>
          </div>
          <p className="text-xs text-gray-600 mb-2">{data.performance.statusExplanation}</p>
          {data.performance.bestRecordedCrop && (
            <div className="flex items-center justify-between mt-2 pt-2 border-t border-gray-100">
              <span className="text-xs text-gray-500">{t('performance.bestCrop')}</span>
              <span className="text-sm font-semibold text-emerald-600">{data.performance.bestRecordedCrop.cropName}</span>
            </div>
          )}
          {data.performance.weakestRecordedCrop && (
            <div className="flex items-center justify-between mt-1">
              <span className="text-xs text-gray-500">{t('performance.weakestCrop')}</span>
              <span className="text-sm font-semibold text-rose-600">{data.performance.weakestRecordedCrop.cropName}</span>
            </div>
          )}
        </Card>

        {/* Notifications */}
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-3">
            <Bell className="h-4 w-4 text-sky-500" />
            <h3 className="text-sm font-semibold text-gray-900">{t('farmDashboard.notificationsSection')}</h3>
            {data.notifications.unreadCount > 0 && (
              <Badge variant="danger" size="sm">{data.notifications.unreadCount}</Badge>
            )}
          </div>
          {data.notifications.recentNotifications.length === 0 ? (
            <p className="text-xs text-gray-400 text-center py-4">{t('farmDashboard.noNotifications')}</p>
          ) : (
            <div className="space-y-2">
              {data.notifications.recentNotifications.slice(0, 4).map((n) => (
                <div key={n.id} className="flex items-start gap-2">
                  <div className={cn('h-2 w-2 rounded-full mt-1.5 shrink-0', n.isRead ? 'bg-gray-300' : 'bg-sky-500')} />
                  <div>
                    <p className="text-xs font-medium text-gray-900">{n.title}</p>
                    <p className="text-[10px] text-gray-500">{formatDate(n.createdAt)}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>

      {/* Limitations */}
      {data.limitations.length > 0 && (
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-2">
            <Info className="h-4 w-4 text-gray-400" />
            <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Limitations</h3>
          </div>
          {data.limitations.map((l, i) => (
            <p key={i} className="text-[10px] text-gray-400">· {l.message}</p>
          ))}
        </Card>
      )}

      <p className="text-[10px] text-gray-400 text-center">{data.disclaimer}</p>
    </div>
  );
}

function StatCard({ icon: Icon, label, value, color }: { icon: React.ElementType; label: string; value: string; color: string }) {
  return (
    <Card padding="sm">
      <div className="flex items-center gap-2 mb-2">
        <Icon className={cn('h-4 w-4', `text-${color}-600`)} />
        <p className="text-[10px] text-gray-400 font-medium uppercase tracking-wider">{label}</p>
      </div>
      <p className="text-lg font-bold text-gray-900">{value}</p>
    </Card>
  );
}

function MiniStat({ label, value, color }: { label: string; value: number; color: string }) {
  return (
    <div>
      <p className="text-[10px] text-gray-400 font-medium">{label}</p>
      <p className={cn('text-xl font-bold', color)}>{value}</p>
    </div>
  );
}
