import { useEffect, useState } from 'react';
import { weatherApi } from '@/api/weatherApi';
import { t } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import {
  Bell, CloudRain, Wind, Snowflake, ThermometerSun, Leaf, ShieldCheck,
} from 'lucide-react';
import type { WeatherAlertDto, WeatherAlertsResponseDto } from '@/types';

interface WeatherAlertsCardProps {
  farmId: string;
  /** Max alerts to render (dashboard shows a compact set). */
  limit?: number;
  /** Render nothing when there are no alerts (avoids dashboard clutter). */
  hideWhenEmpty?: boolean;
}

/** Per-type icon for the alert rows. */
const TYPE_ICONS: Record<string, typeof CloudRain> = {
  RainRisk: CloudRain,
  FungalRisk: Leaf,
  WindAlert: Wind,
  FrostRisk: Snowflake,
  HeatStress: ThermometerSun,
};

/** Severity → row styling (Danger red, Warning amber, Info sky). */
const SEVERITY_STYLES: Record<string, string> = {
  Danger: 'bg-red-50 border-red-200',
  Warning: 'bg-amber-50 border-amber-200',
  Info: 'bg-sky-50 border-sky-200',
};

const SEVERITY_BADGE: Record<string, 'danger' | 'warning' | 'info'> = {
  Danger: 'danger',
  Warning: 'warning',
  Info: 'info',
};

/**
 * Smart weather action alerts (hackathon feature): rule-based alerts derived
 * from the farm forecast (rain runoff risk, fungal pressure, spray-drift
 * wind, frost, heat stress). Fails silently — alerts supplement the page,
 * they never block it.
 */
export function WeatherAlertsCard({ farmId, limit, hideWhenEmpty }: WeatherAlertsCardProps) {
  const [data, setData] = useState<WeatherAlertsResponseDto | null>(null);

  useEffect(() => {
    weatherApi.getAlerts(farmId).then(setData).catch(() => { /* alerts are optional */ });
  }, [farmId]);

  if (!data) return null;

  const alerts = limit ? data.alerts.slice(0, limit) : data.alerts;

  if (alerts.length === 0 && hideWhenEmpty) return null;

  return (
    <Card padding="sm">
      <div className="flex items-center gap-2 mb-3">
        <Bell className="h-4 w-4 text-amber-500" />
        <h3 className="text-sm font-semibold text-gray-900">{t('weather.alertsTitle')}</h3>
        {data.alerts.length > 0 && (
          <Badge variant={data.alerts.some((a) => a.severity === 'Danger') ? 'danger' : 'warning'} size="sm">
            {data.alerts.length}
          </Badge>
        )}
      </div>

      {alerts.length === 0 ? (
        <div className="flex items-center gap-2.5 text-sm text-emerald-700 bg-emerald-50 border border-emerald-100 rounded-xl px-3.5 py-3">
          <ShieldCheck className="h-5 w-5 shrink-0 text-emerald-600" />
          <p className="text-xs">{t('weather.noAlerts')}</p>
        </div>
      ) : (
        <div className="space-y-2">
          {alerts.map((alert, i) => (
            <AlertRow key={`${alert.type}-${alert.when}-${i}`} alert={alert} />
          ))}
        </div>
      )}

      {data.disclaimer && (
        <p className="text-[10px] text-gray-400 mt-3">{data.disclaimer}</p>
      )}
    </Card>
  );
}

function AlertRow({ alert }: { alert: WeatherAlertDto }) {
  const Icon = TYPE_ICONS[alert.type] ?? Bell;
  const severity = alert.severity in SEVERITY_STYLES ? alert.severity : 'Info';

  return (
    <div className={cn('rounded-xl border p-3.5 flex items-start gap-3', SEVERITY_STYLES[severity])}>
      <Icon className="h-5 w-5 shrink-0 mt-0.5 text-gray-600" />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <h4 className="text-sm font-semibold text-gray-900">{alert.title}</h4>
          <Badge variant={SEVERITY_BADGE[severity]} size="sm">{alert.when}</Badge>
        </div>
        <p className="text-xs text-gray-700 mt-1 leading-relaxed">{alert.message}</p>
        {alert.trigger && (
          <p className="text-[10px] text-gray-500 mt-1.5">{alert.trigger}</p>
        )}
      </div>
    </div>
  );
}
