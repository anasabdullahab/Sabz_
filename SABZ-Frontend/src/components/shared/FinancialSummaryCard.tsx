import { cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  TrendingUp,
  TrendingDown,
  Minus,
  DollarSign,
} from 'lucide-react';
import { Card } from '@/components/ui/Card';

interface FinancialSummaryCardProps {
  totalIncome: number;
  totalExpenses: number;
  netResult: number;
  transactionCount?: number;
  className?: string;
}

export function FinancialSummaryCard({
  totalIncome,
  totalExpenses,
  netResult,
  transactionCount,
  className,
}: FinancialSummaryCardProps) {
  const fmt = (n: number) =>
    `PKR ${Math.abs(n).toLocaleString('en-PK', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

  return (
    <Card padding="none" className={cn('overflow-hidden', className)}>
      <div className="grid grid-cols-1 sm:grid-cols-3 divide-y sm:divide-y-0 sm:divide-x divide-gray-100">
        {/* Income */}
        <div className="p-4 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-emerald-50 flex items-center justify-center shrink-0">
            <TrendingUp className="h-5 w-5 text-emerald-600" />
          </div>
          <div>
            <p className="text-xs text-gray-500 font-medium">{t('financial.totalIncome')}</p>
            <p className="text-lg font-bold text-emerald-700">{fmt(totalIncome)}</p>
          </div>
        </div>

        {/* Expenses */}
        <div className="p-4 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-red-50 flex items-center justify-center shrink-0">
            <TrendingDown className="h-5 w-5 text-red-600" />
          </div>
          <div>
            <p className="text-xs text-gray-500 font-medium">{t('financial.totalSpent')}</p>
            <p className="text-lg font-bold text-red-700">{fmt(totalExpenses)}</p>
          </div>
        </div>

        {/* Net Result */}
        <div className="p-4 flex items-center gap-3">
          <div
            className={cn(
              'h-10 w-10 rounded-xl flex items-center justify-center shrink-0',
              netResult > 0 ? 'bg-emerald-50' : netResult < 0 ? 'bg-red-50' : 'bg-gray-50',
            )}
          >
            {netResult > 0 ? (
              <TrendingUp className="h-5 w-5 text-emerald-600" />
            ) : netResult < 0 ? (
              <TrendingDown className="h-5 w-5 text-red-600" />
            ) : (
              <Minus className="h-5 w-5 text-gray-500" />
            )}
          </div>
          <div>
            <p className="text-xs text-gray-500 font-medium">{t('financial.netProfit')}</p>
            <p
              className={cn(
                'text-lg font-bold',
                netResult > 0 ? 'text-emerald-700' : netResult < 0 ? 'text-red-700' : 'text-gray-700',
              )}
            >
              {netResult >= 0 ? fmt(netResult) : `−${fmt(netResult)}`}
            </p>
          </div>
        </div>
      </div>

      {transactionCount !== undefined && transactionCount > 0 && (
        <div className="border-t border-gray-100 px-4 py-2 flex items-center gap-1.5 text-xs text-gray-500">
          <DollarSign className="h-3.5 w-3.5" />
          {t('financial.transactionsRecorded').replace('{count}', String(transactionCount))}
        </div>
      )}
    </Card>
  );
}
