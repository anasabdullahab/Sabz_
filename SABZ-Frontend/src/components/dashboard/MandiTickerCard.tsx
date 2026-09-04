import { useEffect, useState } from 'react';
import { cropPriceApi } from '@/api/cropPriceApi';
import { Card } from '@/components/ui/Card';
import { t } from '@/lib/i18n';
import { TrendingUp } from 'lucide-react';
import type { CropPriceRecordDto } from '@/types';

/**
 * Local Mandi price ticker for the dashboard onboarding layout.
 * Shows the latest crop prices in a horizontally scrolling strip.
 * Renders nothing when the price feed is unavailable — the weather
 * preview card is the visual anchor of the right column.
 */
export function MandiTickerCard() {
  const [items, setItems] = useState<CropPriceRecordDto[]>([]);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    cropPriceApi.getPrices({ page: 1, pageSize: 10 })
      .then((r) => {
        setItems(r.items);
        setVisible(r.items.length > 0);
      })
      .catch(() => setVisible(false));
  }, []);

  if (!visible) return null;

  // Duplicated once for a seamless marquee loop.
  const tickerItems = [...items, ...items];

  return (
    <Card padding="none" className="overflow-hidden">
      <div className="flex items-center gap-2 px-5 py-3 border-b border-gray-100">
        <TrendingUp className="h-4 w-4 text-primary-700" />
        <p className="text-sm font-semibold text-gray-900">{t('dashboard.mandiTicker')}</p>
      </div>
      <div className="relative py-3 overflow-hidden">
        <div className="flex gap-8 whitespace-nowrap animate-ticker w-max">
          {tickerItems.map((item, i) => (
            <span key={`${item.cropName}-${i}`} className="flex items-center gap-1.5 text-xs">
              <span className="font-medium text-gray-800">{item.cropName}</span>
              <span className="font-bold text-primary-700">
                PKR {item.price.toLocaleString('en-PK')}
              </span>
              <span className="text-gray-400">/{item.unit}</span>
            </span>
          ))}
        </div>
        {/* Edge fades */}
        <div className="absolute inset-y-0 left-0 w-6 bg-gradient-to-r from-white to-transparent pointer-events-none" />
        <div className="absolute inset-y-0 right-0 w-6 bg-gradient-to-l from-white to-transparent pointer-events-none" />
      </div>
    </Card>
  );
}
