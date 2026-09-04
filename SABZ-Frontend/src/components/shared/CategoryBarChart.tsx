import { cn } from '@/lib/utils';
import type { HealthCategoryDto } from '@/types';

interface CategoryBarChartProps {
  categories: HealthCategoryDto[];
  total: number;
  color: 'emerald' | 'red' | 'primary';
  title: string;
  className?: string;
}

const colorMap = {
  emerald: 'bg-emerald-500',
  red: 'bg-red-500',
  primary: 'bg-primary-600',
};

const labelColorMap = {
  emerald: 'text-emerald-700',
  red: 'text-red-700',
  primary: 'text-primary-700',
};

export function CategoryBarChart({ categories, total, color, title, className }: CategoryBarChartProps) {
  const fmt = (n: number) =>
    `PKR ${Math.abs(n).toLocaleString('en-PK', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

  if (categories.length === 0) {
    return (
      <div className={cn('p-4 text-center text-sm text-gray-500', className)}>
        No {title.toLowerCase()} data recorded.
      </div>
    );
  }

  return (
    <div className={cn('space-y-3', className)}>
      <h4 className={cn('text-sm font-semibold', labelColorMap[color])}>{title}</h4>
      <div className="space-y-2.5">
        {categories.map((cat) => (
          <div key={cat.category}>
            <div className="flex items-center justify-between text-xs mb-1">
              <span className="font-medium text-gray-700">{cat.category}</span>
              <span className="text-gray-500">
                {fmt(cat.amount)}{' '}
                <span className="text-gray-400">({cat.percentage.toFixed(1)}%)</span>
                {' '}· {cat.transactionCount} tx
              </span>
            </div>
            <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
              <div
                className={cn('h-full rounded-full transition-all duration-500', colorMap[color])}
                style={{ width: `${Math.max(cat.percentage, 1)}%` }}
              />
            </div>
          </div>
        ))}
      </div>
      {total > 0 && (
        <p className="text-xs text-gray-400 pt-1 border-t border-gray-100">
          Total: {fmt(total)}
        </p>
      )}
    </div>
  );
}
