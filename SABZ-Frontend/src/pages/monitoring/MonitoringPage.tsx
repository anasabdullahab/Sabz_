import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { monitoringApi } from '@/api/monitoringApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Badge, monitoringStatusBadge, priorityBadge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Modal } from '@/components/ui/Modal';
import { Alert } from '@/components/ui/Alert';
import { EmptyState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/EmptyState';
import { formatDate, cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  ClipboardCheck, Clock, CheckCircle2, SkipForward, Calendar,
  AlertTriangle, ChevronRight, MapPin, Sprout, Camera,
} from 'lucide-react';
import type { MonitoringCheckDto, MonitoringCompletionResponseDto } from '@/types';

export function MonitoringPage() {
  const [tab, setTab] = useState<'due' | 'upcoming'>('due');
  const [dueChecks, setDueChecks] = useState<MonitoringCheckDto[]>([]);
  const [upcomingChecks, setUpcomingChecks] = useState<MonitoringCheckDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Complete modal
  const [completeTarget, setCompleteTarget] = useState<MonitoringCheckDto | null>(null);
  const [observation, setObservation] = useState<'Normal' | 'SomethingSuspicious'>('Normal');
  const [farmerNotes, setFarmerNotes] = useState('');
  const [completing, setCompleting] = useState(false);
  const [completeResult, setCompleteResult] = useState<MonitoringCompletionResponseDto | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  // Skip modal
  const [skipTarget, setSkipTarget] = useState<MonitoringCheckDto | null>(null);
  const [skipNotes, setSkipNotes] = useState('');
  const [skipping, setSkipping] = useState(false);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [due, upcoming] = await Promise.all([
        monitoringApi.getDue(),
        monitoringApi.getUpcoming(),
      ]);
      setDueChecks(due);
      setUpcomingChecks(upcoming);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleComplete = async () => {
    if (!completeTarget) return;
    setCompleting(true);
    setActionError(null);
    try {
      const res = await monitoringApi.complete(completeTarget.id, {
        observation,
        notes: farmerNotes || null,
      });
      setCompleteResult(res);
      setCompleteTarget(null);
      setObservation('Normal');
      setFarmerNotes('');
      load(); // Refresh
    } catch (err) {
      setActionError(parseApiError(err).message);
    } finally {
      setCompleting(false);
    }
  };

  const handleSkip = async () => {
    if (!skipTarget) return;
    setSkipping(true);
    setActionError(null);
    try {
      await monitoringApi.skip(skipTarget.id, skipNotes || null);
      setSkipTarget(null);
      setSkipNotes('');
      load();
    } catch (err) {
      setActionError(parseApiError(err).message);
    } finally {
      setSkipping(false);
    }
  };

  const checks = tab === 'due' ? dueChecks : upcomingChecks;

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={load} />;

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-primary-500 to-emerald-500 flex items-center justify-center">
            <ClipboardCheck className="h-5 w-5 text-white" />
          </div>
          {t('monitoring.title')}
        </h1>
        <p className="text-gray-500 mt-1 ml-[52px]">{t('monitoring.description')}</p>
      </div>

      {/* Completion result alert */}
      {completeResult && (
        <Alert
          variant={completeResult.photoAnalysisRecommended ? 'warning' : 'success'}
          title={completeResult.check.observation === 'SomethingSuspicious' ? 'Something suspicious reported' : 'Check completed'}
          dismissible
        >
          {completeResult.photoAnalysisRecommended && (
            <p className="mt-1">{t('monitoring.suspiciousNote')}</p>
          )}
          <p className="text-xs mt-1">{completeResult.observationNote}</p>
        </Alert>
      )}

      {/* Summary badges */}
      <div className="flex items-center gap-3">
        <div className="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-50 border border-red-100">
          <AlertTriangle className="h-4 w-4 text-red-500" />
          <span className="text-sm font-semibold text-red-700">{dueChecks.length} Due</span>
        </div>
        <div className="flex items-center gap-2 px-4 py-2 rounded-xl bg-blue-50 border border-blue-100">
          <Clock className="h-4 w-4 text-blue-500" />
          <span className="text-sm font-semibold text-blue-700">{upcomingChecks.length} Upcoming</span>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 p-1 bg-gray-100 rounded-xl w-fit">
        <button
          onClick={() => setTab('due')}
          className={cn(
            'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
            tab === 'due' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700',
          )}
        >
          {t('monitoring.due')} ({dueChecks.length})
        </button>
        <button
          onClick={() => setTab('upcoming')}
          className={cn(
            'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
            tab === 'upcoming' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700',
          )}
        >
          {t('monitoring.upcoming')} ({upcomingChecks.length})
        </button>
      </div>

      {/* Checks list */}
      {checks.length === 0 ? (
        <EmptyState
          icon={<ClipboardCheck className="h-16 w-16" />}
          title={tab === 'due' ? t('monitoring.noDue') : t('monitoring.noUpcoming')}
          description="Monitoring checks are generated based on your crop planting dates."
        />
      ) : (
        <div className="space-y-3">
          {checks.map((check) => (
            <CheckCard
              key={check.id}
              check={check}
              showActions={tab === 'due'}
              onComplete={() => { setCompleteTarget(check); setCompleteResult(null); }}
              onSkip={() => setSkipTarget(check)}
            />
          ))}
        </div>
      )}

      {/* Complete Modal */}
      <Modal open={!!completeTarget} onClose={() => setCompleteTarget(null)} title={t('monitoring.complete')} size="md">
        {completeTarget && (
          <div className="space-y-4">
            <div className="p-3 rounded-lg bg-gray-50 border border-gray-100">
              <p className="text-sm font-medium text-gray-900">{completeTarget.title}</p>
              <p className="text-xs text-gray-500 mt-0.5">{completeTarget.cropName} · {completeTarget.farmName}</p>
            </div>

            {/* Inspection items */}
            {completeTarget.inspectionItems.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-gray-700 mb-1.5">{t('monitoring.inspectionItems')}</p>
                <ul className="space-y-1">
                  {completeTarget.inspectionItems.map((item, i) => (
                    <li key={i} className="text-xs text-gray-600 flex items-start gap-2">
                      <span className="h-1.5 w-1.5 rounded-full bg-primary-500 mt-1.5 shrink-0" />
                      {item}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Observation selection */}
            <div>
              <p className="text-sm font-medium text-gray-700 mb-2">{t('monitoring.observation')}</p>
              <div className="grid grid-cols-2 gap-2">
                <button
                  onClick={() => setObservation('Normal')}
                  className={cn(
                    'p-3 rounded-xl border-2 text-sm font-medium transition-colors text-center',
                    observation === 'Normal'
                      ? 'border-emerald-500 bg-emerald-50 text-emerald-700'
                      : 'border-gray-200 text-gray-600 hover:border-gray-300',
                  )}
                >
                  <CheckCircle2 className="h-5 w-5 mx-auto mb-1" />
                  {t('monitoring.normal')}
                </button>
                <button
                  onClick={() => setObservation('SomethingSuspicious')}
                  className={cn(
                    'p-3 rounded-xl border-2 text-sm font-medium transition-colors text-center',
                    observation === 'SomethingSuspicious'
                      ? 'border-amber-500 bg-amber-50 text-amber-700'
                      : 'border-gray-200 text-gray-600 hover:border-gray-300',
                  )}
                >
                  <AlertTriangle className="h-5 w-5 mx-auto mb-1" />
                  {t('monitoring.suspicious')}
                </button>
              </div>
            </div>

            {/* Notes */}
            <div className="space-y-1.5">
              <label className="block text-sm font-medium text-gray-700">{t('monitoring.farmerNotes')}</label>
              <textarea
                value={farmerNotes}
                onChange={(e) => setFarmerNotes(e.target.value)}
                placeholder={t('monitoring.notesPlaceholder')}
                rows={2}
                className="w-full rounded-xl border border-gray-300 px-4 py-2.5 text-sm bg-white text-gray-900 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-primary-500 hover:border-gray-400 transition-colors resize-none"
              />
            </div>

            <div className="flex justify-end gap-3 pt-2">
              <Button variant="secondary" onClick={() => setCompleteTarget(null)}>{t('common.cancel')}</Button>
              <Button loading={completing} onClick={handleComplete}>{t('monitoring.complete')}</Button>
            </div>
            {actionError && <Alert variant="error">{actionError}</Alert>}
          </div>
        )}
      </Modal>

      {/* Skip Modal */}
      <Modal open={!!skipTarget} onClose={() => setSkipTarget(null)} title={t('monitoring.skip')} size="sm">
        <div className="space-y-4">
          <p className="text-sm text-gray-600">{t('monitoring.skipConfirm')}</p>
          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-gray-700">{t('monitoring.farmerNotes')}</label>
            <textarea
              value={skipNotes}
              onChange={(e) => setSkipNotes(e.target.value)}
              placeholder={t('monitoring.notesPlaceholder')}
              rows={2}
              className="w-full rounded-xl border border-gray-300 px-4 py-2.5 text-sm bg-white text-gray-900 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-primary-500 hover:border-gray-400 transition-colors resize-none"
            />
          </div>
          <div className="flex justify-end gap-3">
            <Button variant="secondary" onClick={() => setSkipTarget(null)}>{t('common.cancel')}</Button>
            <Button variant="danger" loading={skipping} onClick={handleSkip}>{t('monitoring.skip')}</Button>
          </div>
          {actionError && <Alert variant="error">{actionError}</Alert>}
        </div>
      </Modal>
    </div>
  );
}

/* ─── Check Card ─────────────────────────────────────────────────────── */
function CheckCard({
  check,
  showActions,
  onComplete,
  onSkip,
}: {
  check: MonitoringCheckDto;
  showActions: boolean;
  onComplete: () => void;
  onSkip: () => void;
}) {
  const navigate = useNavigate();

  return (
    <Card padding="sm" className="hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1 flex-wrap">
            <Badge variant={monitoringStatusBadge(check.status)} size="sm">{check.status}</Badge>
            <Badge variant={priorityBadge(check.priority)} size="sm">{check.priority}</Badge>
          </div>
          <h3 className="text-sm font-semibold text-gray-900 mt-1">{check.title}</h3>
          <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{check.description}</p>

          <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
            <span className="flex items-center gap-1">
              <Calendar className="h-3 w-3" />
              {formatDate(check.scheduledDate)}
            </span>
            <span className="flex items-center gap-1">
              <Sprout className="h-3 w-3" />
              {check.cropName}
            </span>
            {check.farmName && (
              <span className="flex items-center gap-1">
                <MapPin className="h-3 w-3" />
                {check.farmName}
              </span>
            )}
          </div>

          {/* Completed / Skipped metadata */}
          {check.status === 'Completed' && (
            <div className="mt-2 flex items-center gap-2 text-xs">
              <Badge variant={check.observation === 'SomethingSuspicious' ? 'warning' : 'success'} size="sm">
                {check.observation || 'Completed'}
              </Badge>
              {check.completedAt && (
                <span className="text-gray-400">{formatDate(check.completedAt)}</span>
              )}
              {check.photoAnalysisRecommended && (
                <span className="text-amber-600 flex items-center gap-1">
                  <Camera className="h-3 w-3" /> {t('monitoring.photoRecommended')}
                </span>
              )}
            </div>
          )}

          {check.farmerNotes && check.status !== 'Scheduled' && (
            <p className="text-xs text-gray-500 mt-1 italic">"{check.farmerNotes}"</p>
          )}
        </div>

        {/* Actions */}
        <div className="flex flex-col gap-1.5 shrink-0">
          {showActions && check.status === 'Due' && (
            <>
              <Button size="sm" onClick={onComplete}>
                <CheckCircle2 className="h-3.5 w-3.5" /> {t('monitoring.complete')}
              </Button>
              <Button size="sm" variant="ghost" onClick={onSkip}>
                <SkipForward className="h-3.5 w-3.5" /> {t('monitoring.skip')}
              </Button>
            </>
          )}
          {check.photoAnalysisRecommended && check.status === 'Completed' && (
            <Button
              size="sm"
              variant="outline"
              onClick={() => navigate(`/farms/${check.farmId}/disease-detection`)}
            >
              <Camera className="h-3.5 w-3.5" /> {t('monitoring.analyzePhoto')}
            </Button>
          )}
          <button
            onClick={() => navigate(`/farms/${check.farmId}/crops/${check.cropId}/monitoring`)}
            className="flex items-center gap-1 text-xs text-primary-600 hover:text-primary-800 font-medium"
          >
            Details <ChevronRight className="h-3 w-3" />
          </button>
        </div>
      </div>
    </Card>
  );
}
