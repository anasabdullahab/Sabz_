import { cn } from '@/lib/utils';
import { AlertCircle, CheckCircle2, Info, X } from 'lucide-react';
import { useState } from 'react';

type AlertVariant = 'success' | 'error' | 'warning' | 'info';

interface AlertProps {
  variant?: AlertVariant;
  title?: string;
  children: React.ReactNode;
  dismissible?: boolean;
  className?: string;
}

const variants: Record<AlertVariant, { bg: string; icon: string; border: string }> = {
  success: { bg: 'bg-emerald-50', icon: 'text-emerald-500', border: 'border-emerald-200' },
  error: { bg: 'bg-red-50', icon: 'text-red-500', border: 'border-red-200' },
  warning: { bg: 'bg-amber-50', icon: 'text-amber-500', border: 'border-amber-200' },
  info: { bg: 'bg-blue-50', icon: 'text-blue-500', border: 'border-blue-200' },
};

const icons = {
  success: CheckCircle2,
  error: AlertCircle,
  warning: AlertCircle,
  info: Info,
};

export function Alert({ variant = 'info', title, children, dismissible, className }: AlertProps) {
  const [visible, setVisible] = useState(true);
  if (!visible) return null;

  const v = variants[variant];
  const Icon = icons[variant];

  return (
    <div
      role="alert"
      className={cn('flex gap-3 rounded-xl border p-4', v.bg, v.border, className)}
    >
      <Icon className={cn('h-5 w-5 shrink-0 mt-0.5', v.icon)} />
      <div className="flex-1 text-sm">
        {title && <p className="font-semibold text-gray-900 mb-0.5">{title}</p>}
        <div className="text-gray-700">{children}</div>
      </div>
      {dismissible && (
        <button
          onClick={() => setVisible(false)}
          className="p-0.5 rounded text-gray-400 hover:text-gray-600"
          aria-label="Dismiss"
        >
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}
