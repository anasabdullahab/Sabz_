import { cn } from '@/lib/utils';

type BadgeVariant = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'primary';

interface BadgeProps {
  children: React.ReactNode;
  variant?: BadgeVariant;
  className?: string;
  size?: 'sm' | 'md';
}

const variants: Record<BadgeVariant, string> = {
  success: 'bg-emerald-50 text-emerald-700 ring-emerald-600/20',
  warning: 'bg-amber-50 text-amber-700 ring-amber-600/20',
  danger: 'bg-red-50 text-red-700 ring-red-600/20',
  info: 'bg-blue-50 text-blue-700 ring-blue-600/20',
  neutral: 'bg-gray-50 text-gray-600 ring-gray-500/20',
  primary: 'bg-primary-50 text-primary-700 ring-primary-600/20',
};

const sizes = {
  sm: 'px-2 py-0.5 text-xs',
  md: 'px-2.5 py-1 text-xs',
};

export function Badge({ children, variant = 'neutral', className, size = 'md' }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center font-medium rounded-full ring-1 ring-inset',
        variants[variant],
        sizes[size],
        className,
      )}
    >
      {children}
    </span>
  );
}

/** Map monitoring check status to badge variant */
export function monitoringStatusBadge(status: string): BadgeVariant {
  switch (status) {
    case 'Due': return 'danger';
    case 'Upcoming': return 'info';
    case 'Completed': return 'success';
    case 'Skipped': return 'neutral';
    default: return 'neutral';
  }
}

/** Map monitoring priority to badge variant */
export function priorityBadge(priority: string): BadgeVariant {
  const lower = priority.toLowerCase();
  if (lower === 'high') return 'danger';
  if (lower === 'medium') return 'warning';
  return 'neutral';
}

/** Map health indicator to badge variant */
export function healthIndicatorBadge(indicator: string): BadgeVariant {
  switch (indicator) {
    case 'PositiveNetResult': return 'success';
    case 'BreakEven': return 'primary';
    case 'LossRecorded': return 'danger';
    case 'LimitedData': return 'warning';
    case 'NoData': return 'neutral';
    default: return 'neutral';
  }
}
