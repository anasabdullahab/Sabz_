import { useEffect, useState } from 'react';
import { weatherApi } from '@/api/weatherApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { getWeatherInfo } from '@/lib/weatherCodes';
import { t } from '@/lib/i18n';
import { MapPin, Droplets, Wind, CloudOff } from 'lucide-react';
import type { WeatherPreviewDto } from '@/types';

/**
 * Regional weather preview for the dashboard onboarding layout.
 * Follows the tehsil selected in the Add Your Farm wizard (defaults to a
 * major city until the farmer picks one) — no farm required.
 */
export function WeatherPreviewCard({ tehsilId }: { tehsilId: number | null }) {
  const [data, setData] = useState<WeatherPreviewDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (tehsilId == null) {
      setData(null);
      setError(null);
      return;
    }
    setLoading(true);
    setError(null);
    weatherApi.getPreview(tehsilId)
      .then(setData)
      .catch((err) => {
        setData(null);
        setError(parseApiError(err).message);
      })
      .finally(() => setLoading(false));
  }, [tehsilId]);

  const cw = data?.current;
  const info = getWeatherInfo(cw?.weatherCode ?? null);
  const days = (data?.forecast?.days ?? []).slice(0, 3);
  const Icon = info.icon;

  return (
    <Card padding="none" className="overflow-hidden">
      {/* Header */}
      <div className="bg-gradient-to-r from-sky-500 to-blue-600 px-5 py-4 flex items-center justify-between">
        <div className="flex items-center gap-2 text-white">
          <MapPin className="h-4 w-4" />
          <div>
            <p className="text-sm font-semibold leading-tight">
              {data?.locationName || t('dashboard.weatherPreview')}
            </p>
            <p className="text-[11px] text-sky-100">{t('dashboard.weatherPreview')}</p>
          </div>
        </div>
        {data && <span className="text-[10px] text-sky-100">{data.source}</span>}
      </div>

      <div className="p-5">
        {loading && (
          <div className="h-28 flex items-center justify-center text-gray-400 text-sm">
            {t('common.loading')}
          </div>
        )}

        {!loading && error && (
          <div className="py-8 flex flex-col items-center gap-2 text-gray-400">
            <CloudOff className="h-8 w-8" />
            <p className="text-xs">{t('dashboard.weatherUnavailable')}</p>
          </div>
        )}

        {!loading && !error && data && (
          <>
            {/* Current conditions */}
            <div className="flex items-center gap-4">
              <div className="h-14 w-14 rounded-2xl bg-sky-50 flex items-center justify-center shrink-0">
                <Icon className="h-8 w-8 text-sky-600" />
              </div>
              <div className="flex-1">
                <p className="text-3xl font-bold text-gray-900 leading-none">
                  {cw?.temperature != null ? Math.round(cw.temperature) : '--'}
                  <span className="text-base font-medium text-gray-400 ml-0.5">°C</span>
                </p>
                <p className="text-xs text-gray-500 mt-1">{info.label}</p>
              </div>
              <div className="space-y-1.5 text-xs text-gray-600">
                <span className="flex items-center gap-1.5">
                  <Droplets className="h-3.5 w-3.5 text-sky-500" />
                  {cw?.relativeHumidity != null ? `${Math.round(cw.relativeHumidity)}%` : '--'}
                </span>
                <span className="flex items-center gap-1.5">
                  <Wind className="h-3.5 w-3.5 text-sky-500" />
                  {cw?.windSpeed != null ? `${Math.round(cw.windSpeed)} km/h` : '--'}
                </span>
              </div>
            </div>

            {/* 3-day mini forecast */}
            {days.length > 0 && (
              <div className="grid grid-cols-3 gap-2 mt-5 pt-4 border-t border-gray-100">
                {days.map((day, i) => {
                  const dayInfo = getWeatherInfo(day.weatherCode ?? null);
                  const DayIcon = dayInfo.icon;
                  return (
                    <div key={day.date} className="text-center">
                      <p className="text-[10px] font-medium text-gray-400 uppercase">
                        {i === 0
                          ? t('dashboard.today')
                          : new Date(day.date).toLocaleDateString('en-PK', { weekday: 'short' })}
                      </p>
                      <DayIcon className="h-5 w-5 mx-auto my-1.5 text-gray-500" />
                      <p className="text-xs text-gray-900">
                        {day.tempMax != null ? Math.round(day.tempMax) : '--'}°
                        <span className="text-gray-400 ml-1">
                          {day.tempMin != null ? Math.round(day.tempMin) : '--'}°
                        </span>
                      </p>
                    </div>
                  );
                })}
              </div>
            )}
          </>
        )}
      </div>
    </Card>
  );
}
