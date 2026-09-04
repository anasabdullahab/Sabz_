import { t } from '@/lib/i18n';
import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { CropStageProgress } from '@/lib/cropStages';

/**
 * Visual crop stage progress bar embedded in Active Crop Cards.
 * Shows the elapsed day count, overall percent and the four growth
 * stages (Germination → Vegetative → Flowering → Maturity) with the
 * current stage highlighted.
 */
export function CropStageProgress({ progress }: { progress: CropStageProgress }) {
  return (
    <div className="pt-3 border-t border-gray-100 space-y-2">
      {/* Label row */}
      <div className="flex items-center justify-between text-xs">
        <span className="font-medium text-gray-700">
          {progress.currentStage
            ? `${t(`cropStage.${progress.currentStage.key}`)} · ${t('cropStage.day')} ${progress.dayNumber}/${progress.maturityDays}`
            : `${t('cropStage.day')} ${progress.dayNumber}/${progress.maturityDays}`}
        </span>
        <span className="text-gray-400">{progress.percent}%</span>
      </div>

      {/* Progress bar */}
      <div className="h-2 rounded-full bg-gray-100 overflow-hidden">
        <div
          className="h-full rounded-full bg-gradient-to-r from-primary-500 to-primary-600 transition-all"
          style={{ width: `${progress.percent}%` }}
        />
      </div>

      {/* Stage chips */}
      <div className="flex items-center gap-1">
        {progress.stages.map((stage, i) => (
          <div key={stage.key} className="flex items-center gap-1 flex-1 min-w-0">
            <div
              className={cn(
                'flex items-center justify-center h-5 w-5 rounded-full text-[9px] font-bold shrink-0',
                stage.status === 'done' && 'bg-primary-600 text-white',
                stage.status === 'active' && 'bg-primary-100 text-primary-800 ring-2 ring-primary-500',
                stage.status === 'upcoming' && 'bg-gray-100 text-gray-400',
              )}
              title={`${stage.label}: ${t('cropStage.day')} ${stage.startDay}-${stage.endDay}`}
            >
              {stage.status === 'done' ? <Check className="h-3 w-3" /> : i + 1}
            </div>
            {i < progress.stages.length - 1 && (
              <div
                className={cn(
                  'h-0.5 flex-1 rounded',
                  stage.status === 'done' ? 'bg-primary-300' : 'bg-gray-200',
                )}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
