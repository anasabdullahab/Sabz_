import { useEffect, useState, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { weatherApi } from '@/api/weatherApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { WeatherAlertsCard } from '@/components/weather/WeatherAlertsCard';
import { getWeatherInfo, getWeatherLabel } from '@/lib/weatherCodes';
import { formatDateTime, windDirectionLabel } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import {
  ArrowLeft, Thermometer, Droplets, Wind, Cloud, Eye,
  Sunrise, Sunset, RefreshCw, MapPin, Loader2, Wifi,
  Navigation, AlertTriangle,
} from 'lucide-react';
import type { WeatherResponseDto } from '@/types';

export function WeatherPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();
  const [current, setCurrent] = useState<WeatherResponseDto | null>(null);
  const [forecast, setForecast] = useState<WeatherResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Device GPS override (from "Locate Me" button)
  const [deviceLat, setDeviceLat] = useState<number | undefined>(undefined);
  const [deviceLon, setDeviceLon] = useState<number | undefined>(undefined);
  const [locating, setLocating] = useState(false);
  const [locateError, setLocateError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!farmId) return;
    setLoading(true);
    setError(null);
    try {
      const [c, f] = await Promise.all([
        weatherApi.getCurrent(farmId, deviceLat, deviceLon),
        weatherApi.getForecast(farmId, deviceLat, deviceLon),
      ]);
      setCurrent(c);
      setForecast(f);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  }, [farmId, deviceLat, deviceLon]);

  useEffect(() => { load(); }, [load]);

  const handleLocateMe = () => {
    if (!navigator.geolocation) {
      setLocateError(t('weather.locationError'));
      return;
    }
    setLocating(true);
    setLocateError(null);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setDeviceLat(pos.coords.latitude);
        setDeviceLon(pos.coords.longitude);
        setLocating(false);
      },
      (err) => {
        setLocating(false);
        setLocateError(err.code === 1 ? t('weather.locationDenied') : t('weather.locationError'));
      },
      { enableHighAccuracy: true, timeout: 15000 },
    );
  };

  const clearDeviceLocation = () => {
    setDeviceLat(undefined);
    setDeviceLon(undefined);
  };

  if (loading) return <PageSkeleton />;

  // Error state
  if (error && !current) {
    return (
      <div className="space-y-6 animate-fade-in">
        <button onClick={() => navigate(`/farms/${farmId}`)} className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors">
          <ArrowLeft className="h-4 w-4" /> {t('common.back')}
        </button>
        <div className="rounded-2xl border border-amber-200 bg-amber-50 p-6 text-center space-y-4">
          <div className="mx-auto h-14 w-14 rounded-full bg-amber-100 flex items-center justify-center">
            <MapPin className="h-7 w-7 text-amber-600" />
          </div>
          <div>
            <h3 className="text-base font-semibold text-amber-900">{t('weather.unavailable')}</h3>
            <p className="text-sm text-amber-700 mt-1">{error}</p>
          </div>
          <div className="flex items-center justify-center gap-3">
            <Button variant="secondary" size="sm" onClick={load}>
              <RefreshCw className="h-4 w-4" /> {t('common.retry')}
            </Button>
            <Button size="sm" onClick={handleLocateMe} disabled={locating}>
              <MapPin className="h-4 w-4" /> {t('weather.locateMe')}
            </Button>
          </div>
          {locateError && <p className="text-xs text-red-500">{locateError}</p>}
        </div>
      </div>
    );
  }

  const cw = current?.current;
  const days = forecast?.forecast?.days || [];
  const isStale = current?.isStale || forecast?.isStale;

  return (
    <div className="space-y-6 animate-fade-in">
      <button onClick={() => navigate(`/farms/${farmId}`)} className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors">
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      {/* Stale data banner */}
      {isStale && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 flex items-start gap-3">
          <AlertTriangle className="h-5 w-5 text-amber-500 shrink-0 mt-0.5" />
          <div className="flex-1 min-w-0">
            <h4 className="text-sm font-semibold text-amber-900">{t('weather.staleTitle')}</h4>
            <p className="text-xs text-amber-700 mt-0.5">{current?.staleWarning || t('weather.staleHint')}</p>
            <p className="text-xs text-amber-600 mt-1 flex items-center gap-1">
              <Wifi className="h-3 w-3" /> {t('weather.turnOnInternet')}
            </p>
          </div>
          <Button variant="secondary" size="sm" onClick={load}>
            <RefreshCw className="h-4 w-4" /> {t('weather.refreshNow')}
          </Button>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{t('weather.title')}</h1>

          {/* Location name + source badge */}
          <div className="flex flex-wrap items-center gap-2 mt-1.5">
            {current?.locationName && (
              <span className="inline-flex items-center gap-1 text-sm text-gray-700 font-medium">
                <MapPin className="h-3.5 w-3.5 text-primary-500" />
                {current.locationName}
              </span>
            )}
            {current?.coordinateSource && (
              <CoordinateSourceBadge source={current.coordinateSource} />
            )}
          </div>

          {current && (
            <p className="text-xs text-gray-400 mt-1">
              {t('weather.source')}: {current.source} &middot; {t('weather.lastUpdated')}: {formatDateTime(current.retrievedAt)}
            </p>
          )}
        </div>

        <div className="flex items-center gap-2">
          {/* Locate Me button */}
          {!deviceLat ? (
            <button
              onClick={handleLocateMe}
              disabled={locating}
              className="inline-flex items-center gap-1.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-1.5 text-xs font-medium text-primary-700 hover:bg-primary-100 transition-colors disabled:opacity-50"
            >
              {locating ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Navigation className="h-3.5 w-3.5" />
              )}
              {locating ? t('weather.locating') : t('weather.locateMe')}
            </button>
          ) : (
            <button
              onClick={clearDeviceLocation}
              className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 bg-gray-50 px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100 transition-colors"
              title="Clear device location and use farm default"
            >
              <MapPin className="h-3.5 w-3.5" />
              Use Farm Location
            </button>
          )}
          <Button variant="secondary" size="sm" onClick={load}>
            <RefreshCw className="h-4 w-4" /> {t('common.retry')}
          </Button>
        </div>
      </div>

      {locateError && (
        <p className="text-xs text-red-500 -mt-3">{locateError}</p>
      )}

      {/* Smart weather action alerts (rain/fungal/wind/frost/heat) */}
      <WeatherAlertsCard farmId={farmId!} />

      {/* Current weather hero */}
      {cw && (
        <div className={cn(
          'rounded-2xl p-6 lg:p-8 text-white shadow-lg',
          isStale
            ? 'bg-gradient-to-br from-gray-400 to-gray-500'
            : 'bg-gradient-to-br from-sky-500 to-sky-600',
        )}>
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-6">
            <div>
              <div className="flex items-center gap-3 mb-2">
                {(() => {
                  const Icon = getWeatherInfo(cw.weatherCode).icon;
                  return <Icon className="h-12 w-12" />;
                })()}
                <div>
                  <p className="text-5xl font-bold">
                    {cw.temperature != null ? Math.round(cw.temperature) : '—'}°
                  </p>
                  <p className={isStale ? 'text-gray-200 font-medium' : 'text-sky-100 font-medium'}>
                    {getWeatherLabel(cw.weatherCode)}
                  </p>
                </div>
              </div>
              <p className={isStale ? 'text-gray-200 text-sm' : 'text-sky-100 text-sm'}>
                {t('weather.feelsLike')} {cw.apparentTemperature != null ? Math.round(cw.apparentTemperature) : '—'}°C
              </p>
            </div>

            <div className="grid grid-cols-2 gap-x-8 gap-y-3">
              <WeatherStat icon={Droplets} label={t('weather.humidity')} value={cw.relativeHumidity != null ? `${Math.round(cw.relativeHumidity)}%` : '—'} stale={isStale} />
              <WeatherStat icon={Wind} label={t('weather.wind')} value={cw.windSpeed != null ? `${Math.round(cw.windSpeed)} km/h ${windDirectionLabel(cw.windDirection)}` : '—'} stale={isStale} />
              <WeatherStat icon={Cloud} label={t('weather.cloudCover')} value={cw.cloudCover != null ? `${Math.round(cw.cloudCover)}%` : '—'} stale={isStale} />
              <WeatherStat icon={Eye} label={t('weather.precipitation')} value={cw.precipitation != null ? `${cw.precipitation} mm` : '—'} stale={isStale} />
            </div>
          </div>
          <div className={cn(
            'flex items-center gap-4 mt-4 pt-4 border-t text-xs',
            isStale ? 'border-white/20 text-gray-200' : 'border-white/20 text-sky-100',
          )}>
            <span className="flex items-center gap-1">
              {cw.isDay ? <Sunrise className="h-3.5 w-3.5" /> : <Sunset className="h-3.5 w-3.5" />}
              {cw.isDay ? t('weather.daytime') : t('weather.nighttime')}
            </span>
            {cw.observationTime && <span>{t('weather.observed')}: {formatDateTime(cw.observationTime)}</span>}
          </div>
        </div>
      )}

      {/* 7-day forecast */}
      <div>
        <h2 className="text-lg font-semibold text-gray-900 mb-3">{t('weather.forecast')}</h2>
        {days.length === 0 ? (
          <Card><p className="text-sm text-gray-500 text-center py-4">{t('weather.noForecast')}</p></Card>
        ) : (
          <div className="flex gap-3 overflow-x-auto pb-2 scrollbar-thin">
            {days.map((day, i) => {
              const info = getWeatherInfo(day.weatherCode);
              const Icon = info.icon;
              const dateObj = new Date(day.date + 'T00:00:00');
              const dayName = i === 0 ? t('weather.today') : dateObj.toLocaleDateString('en', { weekday: 'short' });
              const dateStr = dateObj.toLocaleDateString('en', { month: 'short', day: 'numeric' });

              return (
                <div
                  key={day.date}
                  className={cn(
                    'flex-shrink-0 w-32 rounded-2xl border p-4 text-center transition-shadow hover:shadow-md',
                    i === 0 ? 'bg-sky-50 border-sky-200' : 'bg-white border-gray-100',
                    isStale && 'opacity-70',
                  )}
                >
                  <p className="text-xs font-semibold text-gray-900">{dayName}</p>
                  <p className="text-[10px] text-gray-400 mb-2">{dateStr}</p>
                  <Icon className="h-8 w-8 mx-auto mb-2 text-sky-600" />
                  <p className="text-xs text-gray-500 mb-3">{info.label}</p>
                  <div className="flex justify-center gap-2 text-sm font-semibold">
                    <span className="text-red-500">{day.tempMax != null ? Math.round(day.tempMax) : '—'}°</span>
                    <span className="text-sky-500">{day.tempMin != null ? Math.round(day.tempMin) : '—'}°</span>
                  </div>
                  <div className="mt-2 space-y-0.5">
                    {day.precipitationProbability != null && day.precipitationProbability > 0 && (
                      <p className="text-[10px] text-sky-600 flex items-center justify-center gap-0.5">
                        <Droplets className="h-2.5 w-2.5" />
                        {Math.round(day.precipitationProbability)}%
                      </p>
                    )}
                    {day.precipitation != null && day.precipitation > 0 && (
                      <p className="text-[10px] text-gray-400">{day.precipitation} {t('weather.mmRain')}</p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

function CoordinateSourceBadge({ source }: { source: string }) {
  const key = `weather.source${source}` as const;
  const label = t(key) !== key ? t(key) : source;
  const colorMap: Record<string, string> = {
    DeviceGps: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    FarmGps: 'bg-blue-50 text-blue-700 border-blue-200',
    TehsilCentre: 'bg-amber-50 text-amber-700 border-amber-200',
  };
  const colors = colorMap[source] || 'bg-gray-50 text-gray-600 border-gray-200';

  return (
    <span className={cn('inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-medium', colors)}>
      {source === 'DeviceGps' && <Navigation className="h-2.5 w-2.5" />}
      {source === 'FarmGps' && <MapPin className="h-2.5 w-2.5" />}
      {source === 'TehsilCentre' && <MapPin className="h-2.5 w-2.5" />}
      {label}
    </span>
  );
}

function WeatherStat({ icon: Icon, label, value, stale }: { icon: typeof Thermometer; label: string; value: string; stale?: boolean }) {
  return (
    <div className="flex items-center gap-2">
      <Icon className={cn('h-4 w-4', stale ? 'text-gray-300' : 'text-sky-200')} />
      <div>
        <p className={cn('text-xs', stale ? 'text-gray-300' : 'text-sky-200')}>{label}</p>
        <p className="text-sm font-semibold">{value}</p>
      </div>
    </div>
  );
}
