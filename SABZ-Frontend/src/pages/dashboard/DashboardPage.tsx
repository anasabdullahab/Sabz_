import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/hooks/useAuth';
import { farmApi } from '@/api/farmApi';
import { locationApi } from '@/api/locationApi';
import { parseApiError } from '@/api/client';
import { getGreeting } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { FarmForm } from '@/components/forms/FarmForm';
import { WeatherPreviewCard } from '@/components/dashboard/WeatherPreviewCard';
import { MandiTickerCard } from '@/components/dashboard/MandiTickerCard';
import {
  Plus,
  MapPin,
  Cloud,
  Sprout,
  Maximize,
  Droplets,
  Layers,
  ClipboardCheck,
  Bell,
  Users,
} from 'lucide-react';
import type { FarmResponseDto, CreateFarmDto } from '@/types';

export function DashboardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [farms, setFarms] = useState<FarmResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Onboarding state (0-farm accounts)
  const [previewTehsilId, setPreviewTehsilId] = useState<number | null>(null);
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    loadFarms();
  }, []);

  const loadFarms = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await farmApi.getAll();
      setFarms(data);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  // Default the weather preview to Lahore (Punjab) until the farmer picks
  // their own tehsil in the Add Your Farm wizard.
  useEffect(() => {
    if (loading || farms.length > 0) return;
    (async () => {
      try {
        const provinces = await locationApi.getProvinces();
        const punjab = provinces.find((p) => p.name.toLowerCase() === 'punjab') ?? provinces[0];
        if (!punjab) return;
        const districts = await locationApi.getDistricts(punjab.id);
        const lahore = districts.find((d) => d.name.toLowerCase().includes('lahore')) ?? districts[0];
        if (!lahore) return;
        const tehsils = await locationApi.getTehsils(lahore.id);
        const tehsil = tehsils.find((x) => x.latitude != null) ?? tehsils[0];
        if (tehsil) setPreviewTehsilId(tehsil.id);
      } catch { /* preview is a nice-to-have; ignore failures */ }
    })();
  }, [loading, farms.length]);

  const handleCreateFarm = async (data: CreateFarmDto) => {
    setCreating(true);
    try {
      const farm = await farmApi.create(data);
      navigate(`/farms/${farm.id}`);
    } finally {
      setCreating(false);
    }
  };

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={loadFarms} />;

  const greeting = getGreeting();
  const firstName = user?.fullName?.split(' ')[0] || 'Farmer';

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Welcome */}
      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900">
          {greeting}, {firstName}
        </h1>
        <p className="text-gray-500 mt-1">
          {farms.length === 0
            ? t('dashboard.onboardingWelcome')
            : "Here's an overview of your farms"}
        </p>
      </div>

      {farms.length === 0 ? (
        /* ─── Onboarding: 2-column layout for 0-farm accounts ─── */
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
          {/* Left: Add Your Farm wizard */}
          <Card>
            <div className="mb-6">
              <h2 className="text-xl font-bold text-gray-900 flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-xl bg-primary-700 flex items-center justify-center shrink-0">
                  <Sprout className="h-5 w-5 text-white" />
                </div>
                {t('dashboard.onboardingTitle')}
              </h2>
              <p className="text-sm text-gray-500 mt-1.5">
                {t('dashboard.onboardingSubtitle')}
              </p>
            </div>
            <FarmForm
              onSubmit={handleCreateFarm}
              loading={creating}
              submitLabel={t('dashboard.addFirstFarm')}
              onTehsilChange={(tehsil) => setPreviewTehsilId(tehsil.id)}
            />
          </Card>

          {/* Right: regional weather preview + local Mandi price ticker */}
          <div className="space-y-6">
            <WeatherPreviewCard tehsilId={previewTehsilId} />
            <MandiTickerCard />
          </div>
        </div>
      ) : (
        <>
          {/* Quick actions */}
          <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-4 gap-3">
            <QuickAction
              icon={MapPin}
              label={t('nav.farms')}
              color="earth"
              onClick={() => navigate('/farms')}
            />
            <QuickAction
              icon={Users}
              label={t('kisan.title')}
              color="primary"
              onClick={() => navigate('/kisan')}
            />
            <QuickAction
              icon={ClipboardCheck}
              label={t('monitoring.title')}
              color="amber"
              onClick={() => navigate('/monitoring')}
            />
            <QuickAction
              icon={Bell}
              label={t('notifications.title')}
              color="violet"
              onClick={() => navigate('/notifications')}
            />
          </div>

          {/* Farms — single "+ Add Farm" CTA (section header) */}
          <div>
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">{t('dashboard.farmOverview')}</h2>
              <Button
                variant="outline"
                size="sm"
                onClick={() => navigate('/farms/new')}
              >
                <Plus className="h-4 w-4" />
                {t('farm.add')}
              </Button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {farms.map((farm) => (
                <FarmCard key={farm.id} farm={farm} />
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

/* ─── Quick Action Button ─────────────────────────────────────── */
function QuickAction({
  icon: Icon,
  label,
  color,
  disabled,
  onClick,
}: {
  icon: typeof Plus;
  label: string;
  color: string;
  disabled?: boolean;
  onClick: () => void;
}) {
  const colorMap: Record<string, string> = {
    primary: 'bg-primary-50 text-primary-700 hover:bg-primary-100',
    earth: 'bg-earth-100 text-earth-700 hover:bg-earth-200',
    sky: 'bg-sky-50 text-sky-600 hover:bg-sky-100',
    amber: 'bg-amber-50 text-amber-600 hover:bg-amber-100',
    emerald: 'bg-emerald-50 text-emerald-700 hover:bg-emerald-100',
    violet: 'bg-violet-50 text-violet-600 hover:bg-violet-100',
  };

  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`flex flex-col items-center gap-2 p-4 rounded-xl transition-colors ${colorMap[color] || colorMap.primary} ${
        disabled ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer'
      }`}
    >
      <Icon className="h-5 w-5" />
      <span className="text-xs font-medium text-center">{label}</span>
    </button>
  );
}

/* ─── Farm Card ───────────────────────────────────────────────── */
function FarmCard({ farm }: { farm: FarmResponseDto }) {
  const navigate = useNavigate();
  const location = [farm.provinceName, farm.districtName].filter(Boolean).join(', ');

  return (
    <Card hover onClick={() => navigate(`/farms/${farm.id}`)} padding="none">
      {/* Top color bar */}
      <div className="h-1.5 bg-gradient-to-r from-primary-500 to-primary-600 rounded-t-2xl" />

      <div className="p-5">
        {/* Farm header */}
        <div className="flex items-start justify-between mb-3">
          <div>
            <h3 className="font-semibold text-gray-900">{farm.farmName}</h3>
            <div className="flex items-center gap-1.5 text-xs text-gray-500 mt-0.5">
              <MapPin className="h-3 w-3" />
              {location || t('farm.noLocation')}
            </div>
          </div>
          <div className="text-right">
            <span className="text-sm font-semibold text-gray-900">
              {farm.farmSize} {farm.farmSizeUnit}
            </span>
          </div>
        </div>

        {/* Farm details */}
        <div className="grid grid-cols-2 gap-2 mb-4">
          {farm.soilType && (
            <div className="flex items-center gap-1.5 text-xs text-gray-600">
              <Layers className="h-3.5 w-3.5 text-earth-500" />
              {farm.soilType}
            </div>
          )}
          {farm.irrigationType && (
            <div className="flex items-center gap-1.5 text-xs text-gray-600">
              <Droplets className="h-3.5 w-3.5 text-sky-500" />
              {farm.irrigationType}
            </div>
          )}
          {farm.tehsilName && (
            <div className="flex items-center gap-1.5 text-xs text-gray-600">
              <Maximize className="h-3.5 w-3.5 text-gray-400" />
              {farm.tehsilName}
            </div>
          )}
        </div>

        {/* Action buttons */}
        <div className="flex gap-2 pt-3 border-t border-gray-100">
          <ActionBtn
            icon={Cloud}
            label={t('farmDetail.weather')}
            onClick={(e) => { e.stopPropagation(); navigate(`/farms/${farm.id}/weather`); }}
          />
          <ActionBtn
            icon={Sprout}
            label={t('farmDetail.crops')}
            onClick={(e) => { e.stopPropagation(); navigate(`/farms/${farm.id}/crops`); }}
          />
          <ActionBtn
            icon={ClipboardCheck}
            label={t('farmDetail.monitoring')}
            onClick={(e) => { e.stopPropagation(); navigate('/monitoring'); }}
          />
          <ActionBtn
            icon={Bell}
            label="Alerts"
            onClick={(e) => { e.stopPropagation(); navigate('/notifications'); }}
          />
        </div>
      </div>
    </Card>
  );
}

function ActionBtn({
  icon: Icon,
  label,
  onClick,
}: {
  icon: typeof Cloud;
  label: string;
  onClick: (e: React.MouseEvent) => void;
}) {
  return (
    <button
      onClick={onClick}
      className="flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg text-xs font-medium text-gray-500 hover:text-primary-700 hover:bg-primary-50 transition-colors"
    >
      <Icon className="h-3.5 w-3.5" />
      {label}
    </button>
  );
}
