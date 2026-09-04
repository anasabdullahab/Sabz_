import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
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
  ArrowLeft, ClipboardCheck, CheckCircle2, SkipForward, Calendar,
  AlertTriangle, Camera, RefreshCw,
} from 'lucide-react';
import type { MonitoringCheckDto, MonitoringCompletionResponseDto, MonitoringGenerationResultDto } from '@/types';

export function CropMonitoringPage() {
  const { farmId, cropId } = useParams<{ farmId: string; cropId: string }>();
  const navigate = useNavigate();

  const [checks, setChecks] = useState<MonitoringCheckDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [generating, setGenerating] = useState(false);
  const [genResult, setGenResult] = useState<MonitoringGenerationResultDto | null>(null);

  // Complete modal
  const [completeTarget, setCompleteTarget] = useState<MonitoringCheckDto | null>(null);
  const [observation, setObservation] = useState<'Normal' | 'SomethingSuspicious'>('Normal');
  const [farmerNotes, setFarmerNotes] = useState('');
  const [completing, setCompleting] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  // Skip modal
  const [skipTarget, setSkipTarget] = useState<MonitoringCheckDto | null>(null);
  const [skipNotes, setSkipNotes] = useState('');
  const [skipping, setSkipping] = useState(false);

  const load = async () => {
    if (!cropId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await monitoringApi.getChecksForCrop(cropId);
      setChecks(data);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [cropId]);

  const handleGenerate = async () => {
    if (!cropId) return;
    setGenerating(true);
    try {
      const res = await monitoringApi.generateChecks(cropId);
      setGenResult(res);
      load();
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setGenerating(false);
    }
  };

  const handleComplete = async () => {
    if (!completeTarget) return;
    setCompleting(true);
    setActionError(null);
    try {
      await monitoringApi.complete(completeTarget.id, { observation, notes: farmerNotes || null });
      setCompleteTarget(null);
      setObservation('Normal');
      setFarmerNotes('');
      load();
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

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={load} />;

  const dueChecks = checks.filter((c) => c.status === 'Due');
  const upcomingChecks = checks.filter((c) => c.status === 'Upcoming');
  const completedChecks = checks.filter((c) => c.status === 'Completed' || c.status === 'Skipped');

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Back */}
      <button
        onClick={() => navigate(`/farms/${farmId}/crops`)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-primary-500 to-emerald-500 flex items-center justify-center">
              <ClipboardCheck className="h-5 w-5 text-white" />
            </div>
            {t('monitoring.title')}
          </h1>
          {checks.length > 0 && (
            <p className="text-gray-500 mt-1 ml-[52px]">
              {checks[0]?.cropName} · {checks[0]?.farmName}
            </p>
          )}
        </div>
        <Button variant="outline" size="sm" loading={generating} onClick={handleGenerate}>
          <RefreshCw className="h-4 w-4" /> {t('monitoring.generate')}
        </Button>
      </div>

      {/* Generation result */}
      {genResult && (
        <Alert variant="success" title={`${genResult.checksCreated} checks created`} dismissible>
          <p className="text-xs mt-1">{genResult.existingChecks} already existed.</p>
          {genResult.notes.length > 0 && (
            <ul className="text-xs mt-1 space-y-0.5">
              {genResult.notes.map((n, i) => (
                <li key={i}>· {n}</li>
              ))}
            </ul>
          )}
        </Alert>
      )}

      {checks.length === 0 ? (
        <EmptyState
          icon={<ClipboardCheck className="h-16 w-16" />}
          title={t('monitoring.noChecks')}
          description="Click 'Generate Checks' to create monitoring checks based on your crop planting date."
          action={{ label: t('monitoring.generate'), onClick: handleGenerate }}
        />
      ) : (
        <div className="space-y-6">
          {/* Due checks */}
          {dueChecks.length > 0 && (
            <Section title={t('monitoring.due')} count={dueChecks.length} color="red">
              {dueChecks.map((check) => (
                <CheckItem
                  key={check.id}
                  check={check}
                  onComplete={() => { setCompleteTarget(check); }}
                  onSkip={() => setSkipTarget(check)}
                  farmId={farmId!}
                />
              ))}
            </Section>
          )}

          {/* Upcoming */}
          {upcomingChecks.length > 0 && (
            <Section title={t('monitoring.upcoming')} count={upcomingChecks.length} color="blue">
              {upcomingChecks.map((check) => (
                <CheckItem key={check.id} check={check} farmId={farmId!} />
              ))}
            </Section>
          )}

          {/* Completed / Skipped */}
          {completedChecks.length > 0 && (
            <Section title="History" count={completedChecks.length} color="gray">
              {completedChecks.map((check) => (
                <CheckItem key={check.id} check={check} farmId={farmId!} />
              ))}
            </Section>
          )}
        </div>
      )}

      {/* Complete Modal */}
      <Modal open={!!completeTarget} onClose={() => setCompleteTarget(null)} title={t('monitoring.complete')}>
        {completeTarget && (
          <div className="space-y-4">
            <div className="p-3 rounded-lg bg-gray-50 border border-gray-100">
              <p className="text-sm font-medium text-gray-900">{completeTarget.title}</p>
              <p className="text-xs text-gray-500 mt-0.5">{formatDate(completeTarget.scheduledDate)}</p>
            </div>

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

            <textarea
              value={farmerNotes}
              onChange={(e) => setFarmerNotes(e.target.value)}
              placeholder={t('monitoring.notesPlaceholder')}
              rows={2}
              className="w-full rounded-xl border border-gray-300 px-4 py-2.5 text-sm bg-white placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary-500 hover:border-gray-400 transition-colors resize-none"
            />

            <div className="flex justify-end gap-3">
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
          <textarea
            value={skipNotes}
            onChange={(e) => setSkipNotes(e.target.value)}
            placeholder={t('monitoring.notesPlaceholder')}
            rows={2}
            className="w-full rounded-xl border border-gray-300 px-4 py-2.5 text-sm bg-white placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary-500 hover:border-gray-400 transition-colors resize-none"
          />
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

/* ─── Section ────────────────────────────────────────────────────────── */
function Section({ title, count, color, children }: {
  title: string;
  count: number;
  color: 'red' | 'blue' | 'gray';
  children: React.ReactNode;
}) {
  const colors = {
    red: 'text-red-700 bg-red-50',
    blue: 'text-blue-700 bg-blue-50',
    gray: 'text-gray-700 bg-gray-50',
  };
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-900 mb-3 flex items-center gap-2">
        <span className={cn('px-2 py-0.5 rounded-full text-xs font-bold', colors[color])}>{count}</span>
        {title}
      </h2>
      <div className="space-y-2">
        {children}
      </div>
    </div>
  );
}

/* ─── Check Item ─────────────────────────────────────────────────────── */
function CheckItem({ check, onComplete, onSkip, farmId }: {
  check: MonitoringCheckDto;
  onComplete?: () => void;
  onSkip?: () => void;
  farmId: string;
}) {
  const navigate = useNavigate();
  const isDue = check.status === 'Due';

  return (
    <Card padding="sm" className="hover:shadow-sm transition-shadow">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1 flex-wrap">
            <Badge variant={monitoringStatusBadge(check.status)} size="sm">{check.status}</Badge>
            <Badge variant={priorityBadge(check.priority)} size="sm">{check.priority}</Badge>
          </div>
          <h4 className="text-sm font-medium text-gray-900">{check.title}</h4>
          <div className="flex items-center gap-3 mt-1 text-xs text-gray-400">
            <span className="flex items-center gap-1">
              <Calendar className="h-3 w-3" /> {formatDate(check.scheduledDate)}
            </span>
          </div>

          {check.status === 'Completed' && check.observation && (
            <div className="mt-2 flex items-center gap-2 text-xs">
              <Badge variant={check.observation === 'SomethingSuspicious' ? 'warning' : 'success'} size="sm">
                {check.observation}
              </Badge>
              {check.photoAnalysisRecommended && (
                <span className="text-amber-600 flex items-center gap-1">
                  <Camera className="h-3 w-3" /> Photo recommended
                </span>
              )}
            </div>
          )}
          {check.farmerNotes && (
            <p className="text-xs text-gray-500 mt-1 italic">"{check.farmerNotes}"</p>
          )}
        </div>

        <div className="flex flex-col gap-1.5 shrink-0">
          {isDue && onComplete && onSkip && (
            <>
              <Button size="sm" onClick={onComplete}>
                <CheckCircle2 className="h-3.5 w-3.5" /> Complete
              </Button>
              <Button size="sm" variant="ghost" onClick={onSkip}>
                <SkipForward className="h-3.5 w-3.5" /> Skip
              </Button>
            </>
          )}
          {check.photoAnalysisRecommended && check.status === 'Completed' && (
            <Button
              size="sm"
              variant="outline"
              onClick={() => navigate(`/farms/${farmId}/disease-detection`)}
            >
              <Camera className="h-3.5 w-3.5" /> {t('monitoring.analyzePhoto')}
            </Button>
          )}
        </div>
      </div>
    </Card>
  );
}
