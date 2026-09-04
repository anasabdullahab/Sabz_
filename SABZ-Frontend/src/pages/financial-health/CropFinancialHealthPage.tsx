import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { financialHealthApi } from '@/api/financialHealthApi';
import { parseApiError } from '@/api/client';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { FinancialSummaryCard } from '@/components/shared/FinancialSummaryCard';
import { formatDate } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { ArrowLeft, Sprout } from 'lucide-react';
import type { FinancialHealthSummaryDto } from '@/types';

export function CropFinancialHealthPage() {
  const { farmId, cropId } = useParams<{ farmId: string; cropId: string }>();
  const navigate = useNavigate();

  const [health, setHealth] = useState<FinancialHealthSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    if (!farmId || !cropId) return;
    setLoading(true);
    setError(null);
    try {
      const res = await financialHealthApi.getCropHealth(farmId, cropId);
      setHealth(res);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [farmId, cropId]);

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={load} />;

  return (
    <div className="space-y-6 animate-fade-in">
      <button
        onClick={() => navigate(`/farms/${farmId}/crops`)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-primary-500 to-emerald-500 flex items-center justify-center">
            <Sprout className="h-5 w-5 text-white" />
          </div>
          {t('health.cropFinancial')}
        </h1>
        <p className="text-gray-500 mt-1 ml-[52px]">Crop-level income & expenses</p>
      </div>

      {health && health.totalTransactionCount > 0 ? (
        <>
          <FinancialSummaryCard
            totalIncome={health.totalIncome}
            totalExpenses={health.totalExpense}
            netResult={health.netResult}
            transactionCount={health.totalTransactionCount}
          />

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div>
              <p className="text-[10px] text-gray-400 font-medium">{t('health.activeDays')}</p>
              <p className="text-sm font-semibold text-gray-900">{health.numberOfActiveFinancialDays}</p>
            </div>
            <div>
              <p className="text-[10px] text-gray-400 font-medium">{t('health.totalTransactions')}</p>
              <p className="text-sm font-semibold text-gray-900">{health.totalTransactionCount}</p>
            </div>
            {health.firstTransactionDate && (
              <div>
                <p className="text-[10px] text-gray-400 font-medium">{t('health.firstTransaction')}</p>
                <p className="text-sm font-semibold text-gray-900">{formatDate(health.firstTransactionDate)}</p>
              </div>
            )}
            {health.lastTransactionDate && (
              <div>
                <p className="text-[10px] text-gray-400 font-medium">{t('health.lastTransaction')}</p>
                <p className="text-sm font-semibold text-gray-900">{formatDate(health.lastTransactionDate)}</p>
              </div>
            )}
          </div>
        </>
      ) : (
        <EmptyState
          icon={<Sprout className="h-16 w-16" />}
          title={t('health.noData')}
          description="Link transactions to this crop to see its financial health."
        />
      )}
    </div>
  );
}
