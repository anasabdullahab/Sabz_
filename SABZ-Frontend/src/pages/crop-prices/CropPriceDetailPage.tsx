import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { cropPriceApi } from '@/api/cropPriceApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { formatDate } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { ArrowLeft, TrendingUp, MapPin, Store, Calendar, AlertTriangle } from 'lucide-react';
import type { CropPriceDetailDto, CropPriceRecordDto } from '@/types';

export function CropPriceDetailPage() {
  const { cropName } = useParams<{ cropName: string }>();
  const navigate = useNavigate();
  const [detail, setDetail] = useState<CropPriceDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!cropName) return;
    cropPriceApi.getPriceByCrop(decodeURIComponent(cropName))
      .then(setDetail)
      .catch((err) => setError(parseApiError(err).message))
      .finally(() => setLoading(false));
  }, [cropName]);

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} />;
  if (!detail) return null;

  return (
    <div className="space-y-6 animate-fade-in max-w-4xl mx-auto">
      <button
        onClick={() => navigate('/crop-prices')}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-orange-500 to-amber-600 flex items-center justify-center">
            <TrendingUp className="h-5 w-5 text-white" />
          </div>
          {detail.cropName}
        </h1>
        <Badge variant={detail.cropRecognized ? 'success' : 'warning'} size="sm">
          {detail.cropRecognized ? 'Recognized' : t('cropPrices.unrecognized')}
        </Badge>
      </div>

      {detail.message && (
        <Card padding="sm">
          <p className="text-sm text-amber-600 flex items-center gap-2">
            <AlertTriangle className="h-4 w-4 shrink-0" /> {detail.message}
          </p>
        </Card>
      )}

      {/* Latest Price */}
      {detail.latest && (
        <Card padding="md">
          <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-4">{t('cropPrices.latestPrice')}</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-primary-50 rounded-xl p-3">
              <p className="text-[10px] font-medium text-primary-600 uppercase tracking-wider">Price</p>
              <p className="text-xl font-bold text-primary-800">
                PKR {detail.latest.price.toLocaleString('en-PK')}
                <span className="text-xs text-primary-500 font-normal ml-1">/{detail.latest.unit}</span>
              </p>
            </div>
            <div>
              <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider">{t('cropPrices.market')}</p>
              <p className="text-sm font-semibold text-gray-900 flex items-center gap-1"><Store className="h-3 w-3 text-gray-400" />{detail.latest.market}</p>
            </div>
            <div>
              <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider">Location</p>
              <p className="text-sm font-semibold text-gray-900 flex items-center gap-1">
                <MapPin className="h-3 w-3 text-gray-400" />
                {[detail.latest.district, detail.latest.province].filter(Boolean).join(', ')}
              </p>
            </div>
            <div>
              <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider">{t('cropPrices.priceDate')}</p>
              <p className="text-sm font-semibold text-gray-900 flex items-center gap-1">
                <Calendar className="h-3 w-3 text-gray-400" />{formatDate(detail.latest.priceDate)}
              </p>
            </div>
          </div>
        </Card>
      )}

      {/* Historical Records */}
      {detail.historicalRecords.length > 0 && (
        <div>
          <h3 className="text-sm font-semibold text-gray-900 mb-3">{t('cropPrices.historical')} ({detail.historicalRecords.length})</h3>
          {detail.firstDate && detail.latestDate && (
            <p className="text-xs text-gray-500 mb-3">
              {formatDate(detail.firstDate)} — {formatDate(detail.latestDate)}
            </p>
          )}
          <Card padding="none">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-200">
                    <th className="text-left py-2 px-4 text-[10px] font-medium text-gray-500 uppercase tracking-wider">Date</th>
                    <th className="text-left py-2 px-4 text-[10px] font-medium text-gray-500 uppercase tracking-wider">Price</th>
                    <th className="text-left py-2 px-4 text-[10px] font-medium text-gray-500 uppercase tracking-wider">Market</th>
                    <th className="text-left py-2 px-4 text-[10px] font-medium text-gray-500 uppercase tracking-wider hidden md:table-cell">Location</th>
                    <th className="text-left py-2 px-4 text-[10px] font-medium text-gray-500 uppercase tracking-wider hidden lg:table-cell">Source</th>
                    <th className="text-left py-2 px-4 text-[10px] font-medium text-gray-500 uppercase tracking-wider">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.historicalRecords.map((record, i) => (
                    <tr key={`${record.market}-${record.priceDate}-${i}`} className="border-b border-gray-50">
                      <td className="py-2.5 px-4 text-gray-600">{formatDate(record.priceDate)}</td>
                      <td className="py-2.5 px-4">
                        <span className="font-bold text-primary-700">PKR {record.price.toLocaleString('en-PK')}</span>
                        <span className="text-xs text-gray-400 ml-1">/{record.unit}</span>
                      </td>
                      <td className="py-2.5 px-4 text-gray-600">{record.market}</td>
                      <td className="py-2.5 px-4 text-gray-600 hidden md:table-cell">
                        {[record.district, record.province].filter(Boolean).join(', ')}
                      </td>
                      <td className="py-2.5 px-4 text-gray-500 text-xs hidden lg:table-cell">{record.source}</td>
                      <td className="py-2.5 px-4">
                        <Badge variant={record.dataStatus === 'Live' ? 'success' : 'warning'} size="sm">
                          {record.dataStatus}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      )}

      {detail.disclaimer && (
        <p className="text-[10px] text-gray-400 text-center">{detail.disclaimer}</p>
      )}
    </div>
  );
}
