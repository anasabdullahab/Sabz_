import { useEffect, useState, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { financialApi } from '@/api/financialApi';
import { farmApi } from '@/api/farmApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Modal } from '@/components/ui/Modal';
import { Alert } from '@/components/ui/Alert';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { FinancialSummaryCard } from '@/components/shared/FinancialSummaryCard';
import { categoryLabel } from '@/lib/financialCategories';
import { formatDate, cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  ArrowLeft, DollarSign, Trash2, Edit, Receipt,
  TrendingUp, TrendingDown, Sprout,
} from 'lucide-react';
import type {
  FinancialTransactionResponseDto,
  FinancialSummaryResponseDto,
  FarmResponseDto,
} from '@/types';

export function FinancialLedgerPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();

  const [farm, setFarm] = useState<FarmResponseDto | null>(null);
  const [transactions, setTransactions] = useState<FinancialTransactionResponseDto[]>([]);
  const [summary, setSummary] = useState<FinancialSummaryResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Delete
  const [deleteTarget, setDeleteTarget] = useState<FinancialTransactionResponseDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!farmId) return;
    setLoading(true);
    setError(null);
    try {
      const [farmData, txData, summaryData] = await Promise.all([
        farmApi.getById(farmId),
        financialApi.getByFarm(farmId, { take: 200 }),
        financialApi.getSummary(farmId),
      ]);
      setFarm(farmData);
      setTransactions(txData);
      setSummary(summaryData);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  }, [farmId]);

  useEffect(() => { load(); }, [load]);

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    setDeleteError(null);
    try {
      await financialApi.delete(deleteTarget.id);
      setDeleteTarget(null);
      load();
    } catch (err) {
      setDeleteError(parseApiError(err).message);
    } finally {
      setDeleting(false);
    }
  };

  const fmt = (n: number) =>
    `PKR ${Math.abs(n).toLocaleString('en-PK', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={load} />;

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Back */}
      <button
        onClick={() => navigate(`/farms/${farmId}`)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center">
              <DollarSign className="h-5 w-5 text-white" />
            </div>
            {t('financial.title')}
          </h1>
          {farm && (
            <p className="text-gray-500 mt-1 ml-[52px]">{farm.farmName} · {t('financial.description')}</p>
          )}
        </div>
        <div className="flex gap-2">
          <Button
            variant="secondary"
            onClick={() => navigate(`/farms/${farmId}/financial/new?type=Expense`)}
          >
            <TrendingDown className="h-4 w-4" /> {t('financial.addExpense')}
          </Button>
          <Button onClick={() => navigate(`/farms/${farmId}/financial/new?type=Income`)}>
            <TrendingUp className="h-4 w-4" /> {t('financial.addIncome')}
          </Button>
        </div>
      </div>

      {/* Summary */}
      {summary && (
        <FinancialSummaryCard
          totalIncome={summary.totalIncome}
          totalExpenses={summary.totalExpenses}
          netResult={summary.netProfitLoss}
          transactionCount={summary.transactionCount}
        />
      )}

      {/* Transaction list */}
      {transactions.length === 0 ? (
        <EmptyState
          icon={<Receipt className="h-16 w-16" />}
          title={t('financial.noTransactions')}
          action={{
            label: t('financial.addExpense'),
            onClick: () => navigate(`/farms/${farmId}/financial/new?type=Expense`),
          }}
        />
      ) : (
        <div className="space-y-2">
          {transactions.map((tx) => (
            <Card key={tx.id} padding="sm" className="hover:shadow-sm transition-shadow">
              <div className="flex items-center justify-between gap-3">
                <div className="flex items-center gap-3 flex-1 min-w-0">
                  <div className={cn(
                    'h-9 w-9 rounded-lg flex items-center justify-center shrink-0',
                    tx.transactionType === 'Income' ? 'bg-emerald-50' : 'bg-red-50',
                  )}>
                    {tx.transactionType === 'Income' ? (
                      <TrendingUp className="h-4 w-4 text-emerald-600" />
                    ) : (
                      <TrendingDown className="h-4 w-4 text-red-600" />
                    )}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-0.5">
                      <span className="text-sm font-semibold text-gray-900">
                        {categoryLabel(tx.category)}
                      </span>
                      <Badge variant={tx.transactionType === 'Income' ? 'success' : 'danger'} size="sm">
                        {tx.transactionType}
                      </Badge>
                    </div>
                    <div className="flex items-center gap-2 text-xs text-gray-400">
                      <span>{formatDate(tx.transactionDate)}</span>
                      {tx.cropName && (
                        <span className="flex items-center gap-1">
                          <Sprout className="h-3 w-3" /> {tx.cropName}
                        </span>
                      )}
                      {tx.notes && (
                        <span className="text-gray-500 truncate max-w-[200px]">· {tx.notes}</span>
                      )}
                    </div>
                  </div>
                </div>

                <div className="flex items-center gap-3 shrink-0">
                  <span className={cn(
                    'text-sm font-bold',
                    tx.transactionType === 'Income' ? 'text-emerald-700' : 'text-red-700',
                  )}>
                    {tx.transactionType === 'Income' ? '+' : '−'}{fmt(tx.amount)}
                  </span>
                  <div className="flex gap-1">
                    <button
                      onClick={() => navigate(`/farms/${farmId}/financial/${tx.id}/edit`)}
                      className="p-1.5 rounded-lg text-gray-400 hover:text-primary-600 hover:bg-primary-50 transition-colors"
                      title={t('common.edit')}
                    >
                      <Edit className="h-3.5 w-3.5" />
                    </button>
                    <button
                      onClick={() => setDeleteTarget(tx)}
                      className="p-1.5 rounded-lg text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors"
                      title={t('common.delete')}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      {/* Delete Modal */}
      <Modal open={!!deleteTarget} onClose={() => setDeleteTarget(null)} title={t('common.delete')} size="sm">
        <div className="space-y-4">
          <p className="text-sm text-gray-600">{t('financial.deleteConfirm')}</p>
          {deleteTarget && (
            <div className="p-3 rounded-lg bg-gray-50 border border-gray-100">
              <p className="text-sm font-medium text-gray-900">
                {categoryLabel(deleteTarget.category)}
              </p>
              <p className="text-xs text-gray-500">
                {fmt(deleteTarget.amount)} · {formatDate(deleteTarget.transactionDate)}
              </p>
            </div>
          )}
          <div className="flex justify-end gap-3">
            <Button variant="secondary" onClick={() => setDeleteTarget(null)}>{t('common.cancel')}</Button>
            <Button variant="danger" loading={deleting} onClick={handleDelete}>{t('common.delete')}</Button>
          </div>
          {deleteError && <Alert variant="error">{deleteError}</Alert>}
        </div>
      </Modal>
    </div>
  );
}
