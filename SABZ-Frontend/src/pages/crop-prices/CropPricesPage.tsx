import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { cropPriceApi, type CropPriceFilters } from '@/api/cropPriceApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { formatDate } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { TrendingUp, Search, MapPin, Store, Calendar, XCircle } from 'lucide-react';
import type { CropPriceRecordDto, CropPricePagedResultDto } from '@/types';

export function CropPricesPage() {
  const navigate = useNavigate();
  const [result, setResult] = useState<CropPricePagedResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [crop, setCrop] = useState('');
  const [province, setProvince] = useState('');
  const [district, setDistrict] = useState('');
  const [market, setMarket] = useState('');
  const [page, setPage] = useState(1);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const filters: CropPriceFilters = { page, pageSize: 20 };
      if (crop.trim()) filters.crop = crop.trim();
      if (province) filters.province = province;
      if (district) filters.district = district;
      if (market) filters.market = market;
      const data = await cropPriceApi.getPrices(filters);
      setResult(data);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  }, [crop, province, district, market, page]);

  useEffect(() => { load(); }, [load]);

  const clearFilters = () => {
    setCrop('');
    setProvince('');
    setDistrict('');
    setMarket('');
    setPage(1);
  };

  const hasFilters = crop || province || district || market;

  if (loading && !result) return <PageSkeleton />;
  if (error && !result) return <ErrorState message={error} onRetry={load} />;

  return (
    <div className="space-y-6 animate-fade-in">
      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-orange-500 to-amber-600 flex items-center justify-center">
            <TrendingUp className="h-5 w-5 text-white" />
          </div>
          {t('cropPrices.title')}
        </h1>
        <p className="text-gray-500 mt-1 ml-[52px]">{t('cropPrices.description')}</p>
      </div>

      {/* Filters */}
      <Card padding="sm">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex-1 min-w-[200px]">
            <label className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-1 block">{t('common.search')}</label>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
              <input
                type="text"
                value={crop}
                onChange={(e) => { setCrop(e.target.value); setPage(1); }}
                placeholder={t('cropPrices.searchCrop')}
                className="w-full pl-9 pr-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
            </div>
          </div>
          <div className="w-40">
            <label className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-1 block">{t('farm.province')}</label>
            <input
              value={province}
              onChange={(e) => { setProvince(e.target.value); setPage(1); }}
              placeholder={t('cropPrices.allProvinces')}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          <div className="w-36">
            <label className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-1 block">{t('farm.district')}</label>
            <input
              value={district}
              onChange={(e) => { setDistrict(e.target.value); setPage(1); }}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          <div className="w-36">
            <label className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-1 block">{t('cropPrices.market')}</label>
            <input
              value={market}
              onChange={(e) => { setMarket(e.target.value); setPage(1); }}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          {hasFilters && (
            <button onClick={clearFilters} className="flex items-center gap-1 text-xs text-gray-500 hover:text-red-500 transition-colors">
              <XCircle className="h-3.5 w-3.5" /> {t('marketplace.clearFilters')}
            </button>
          )}
        </div>
      </Card>

      {result && result.dataStatus && (
        <p className="text-xs text-gray-500 ml-1">{result.dataStatus}</p>
      )}

      {/* Results */}
      {result && result.items.length === 0 ? (
        <EmptyState
          icon={<TrendingUp className="h-16 w-16" />}
          title={t('cropPrices.noData')}
        />
      ) : result && (
        <div className="space-y-3">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200">
                  <th className="text-left py-2 px-3 text-[10px] font-medium text-gray-500 uppercase tracking-wider">Crop</th>
                  <th className="text-left py-2 px-3 text-[10px] font-medium text-gray-500 uppercase tracking-wider">Price</th>
                  <th className="text-left py-2 px-3 text-[10px] font-medium text-gray-500 uppercase tracking-wider hidden md:table-cell">Market</th>
                  <th className="text-left py-2 px-3 text-[10px] font-medium text-gray-500 uppercase tracking-wider hidden md:table-cell">District</th>
                  <th className="text-left py-2 px-3 text-[10px] font-medium text-gray-500 uppercase tracking-wider hidden lg:table-cell">Province</th>
                  <th className="text-left py-2 px-3 text-[10px] font-medium text-gray-500 uppercase tracking-wider hidden lg:table-cell">Date</th>
                  <th className="text-left py-2 px-3 text-[10px] font-medium text-gray-500 uppercase tracking-wider">Status</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((record, i) => (
                  <PriceRow key={`${record.cropName}-${record.market}-${record.priceDate}-${i}`} record={record} onClick={() => navigate(`/crop-prices/${encodeURIComponent(record.cropName)}`)} />
                ))}
              </tbody>
            </table>
          </div>

          {result.totalPages > 1 && (
            <div className="flex justify-center gap-2 pt-4">
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                Previous
              </Button>
              <span className="flex items-center text-xs text-gray-500">{result.page} / {result.totalPages}</span>
              <Button variant="secondary" size="sm" disabled={page >= result.totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          )}

          {result.disclaimer && (
            <p className="text-[10px] text-gray-400 text-center">{result.disclaimer}</p>
          )}
        </div>
      )}
    </div>
  );
}

function PriceRow({ record, onClick }: { record: CropPriceRecordDto; onClick: () => void }) {
  return (
    <tr
      onClick={onClick}
      className="border-b border-gray-50 hover:bg-gray-50 cursor-pointer transition-colors"
    >
      <td className="py-3 px-3">
        <span className="font-medium text-gray-900">{record.cropName}</span>
      </td>
      <td className="py-3 px-3">
        <span className="font-bold text-primary-700">
          PKR {record.price.toLocaleString('en-PK')}
        </span>
        <span className="text-xs text-gray-400 ml-1">/{record.unit}</span>
      </td>
      <td className="py-3 px-3 text-gray-600 hidden md:table-cell">
        <span className="flex items-center gap-1"><Store className="h-3 w-3 text-gray-400" />{record.market}</span>
      </td>
      <td className="py-3 px-3 text-gray-600 hidden md:table-cell">{record.district}</td>
      <td className="py-3 px-3 text-gray-600 hidden lg:table-cell">
        <span className="flex items-center gap-1"><MapPin className="h-3 w-3 text-gray-400" />{record.province}</span>
      </td>
      <td className="py-3 px-3 text-gray-500 text-xs hidden lg:table-cell">
        <span className="flex items-center gap-1"><Calendar className="h-3 w-3" />{formatDate(record.priceDate)}</span>
      </td>
      <td className="py-3 px-3">
        <Badge variant={record.dataStatus === 'Live' ? 'success' : 'warning'} size="sm">
          {record.dataStatus}
        </Badge>
      </td>
    </tr>
  );
}
