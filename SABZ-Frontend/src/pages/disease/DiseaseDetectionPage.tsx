import { useState, useRef, useCallback, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { diseaseApi } from '@/api/diseaseApi';
import { cropApi } from '@/api/cropApi';
import { farmApi } from '@/api/farmApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Select } from '@/components/ui/Select';
import { Badge } from '@/components/ui/Badge';
import { Alert } from '@/components/ui/Alert';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/EmptyState';
import { formatDate, cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  ArrowLeft, Upload, X, Camera, ScanSearch, Leaf, ShieldCheck,
  AlertTriangle, CheckCircle2, Info, Microscope, Activity, Stethoscope,
  Sparkles,
} from 'lucide-react';
import { analyzeLeafImage, isVisionAiConfigured } from '@/lib/geminiVision';
import type { LeafGuardResult } from '@/lib/geminiVision';
import type { DiseaseDetectionResponseDto, DiseaseAdviceDto, CropResponseDto, FarmResponseDto } from '@/types';

const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export function DiseaseDetectionPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();

  // Farm & crop data
  const [farm, setFarm] = useState<FarmResponseDto | null>(null);
  const [crops, setCrops] = useState<CropResponseDto[]>([]);
  const [pageLoading, setPageLoading] = useState(true);
  const [pageError, setPageError] = useState<string | null>(null);

  // Form state
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [cropId, setCropId] = useState('');
  const [notes, setNotes] = useState('');
  const [dragOver, setDragOver] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);

  // Submission — result is the backend response (used when the Gemini key is
  // absent); leafResult is the Gemini leaf-guard outcome; aiFailed marks a
  // Gemini call that failed or timed out (graceful notice only).
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<DiseaseDetectionResponseDto | null>(null);
  const [leafResult, setLeafResult] = useState<LeafGuardResult | null>(null);
  const [aiFailed, setAiFailed] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!farmId) return;
    Promise.all([farmApi.getById(farmId), cropApi.getByFarm(farmId)])
      .then(([f, c]) => { setFarm(f); setCrops(c); })
      .catch((err) => setPageError(parseApiError(err).message))
      .finally(() => setPageLoading(false));
  }, [farmId]);

  // Tier 1 — fast client-side rejection: valid image type + under 10 MB.
  const validateFile = useCallback((f: File): boolean => {
    if (!ACCEPTED_TYPES.includes(f.type) || f.size > MAX_FILE_SIZE) {
      setFileError(t('disease.invalidFile'));
      return false;
    }
    setFileError(null);
    return true;
  }, []);

  const handleFile = useCallback((f: File) => {
    if (!validateFile(f)) return;
    setFile(f);
    const reader = new FileReader();
    reader.onload = (e) => setPreview(e.target?.result as string);
    reader.readAsDataURL(f);
  }, [validateFile]);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const f = e.dataTransfer.files[0];
    if (f) handleFile(f);
  }, [handleFile]);

  const removeFile = () => {
    setFile(null);
    setPreview(null);
    setFileError(null);
    if (inputRef.current) inputRef.current.value = '';
  };

  const handleSubmit = async () => {
    if (!file || !farmId) return;
    const visionMode = isVisionAiConfigured();
    setSubmitting(true);
    setSubmitError(null);
    setResult(null);
    setLeafResult(null);
    setAiFailed(false);
    try {
      if (visionMode) {
        // Tier 2 — AI vision leaf guard (Gemini direct or OpenRouter chain).
        const selectedCrop = crops.find((c) => c.id === cropId);
        const hint = [selectedCrop?.cropName, notes].filter(Boolean).join(' — ');
        const res = await analyzeLeafImage(file, hint || undefined);
        setLeafResult(res);
        if (!res.isPlantLeaf) {
          // Rejected: clear the preview so the farmer can retake / re-upload.
          removeFile();
        }
      } else {
        // No vision provider configured — the SABZ backend endpoint handles it.
        const res = await diseaseApi.detect(farmId, file, cropId || null, notes || null);
        setResult(res);
      }
    } catch (err) {
      if (visionMode) {
        // Never surface raw technical errors — one graceful message.
        console.error('Leaf guard analysis failed:', err);
        setAiFailed(true);
      } else {
        setSubmitError(parseApiError(err).message);
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (pageLoading) return <PageSkeleton />;
  if (pageError) return <ErrorState message={pageError} />;

  const activeCrops = crops.filter((c) => c.status?.toLowerCase() !== 'harvested');

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Back nav */}
      <button
        onClick={() => navigate(`/farms/${farmId}`)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      {/* Header */}
      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-rose-500 to-orange-500 flex items-center justify-center">
            <ScanSearch className="h-5 w-5 text-white" />
          </div>
          {t('disease.title')}
        </h1>
        <p className="text-gray-500 mt-1 ml-[52px]">{t('disease.description')}</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left column: Upload form */}
        <div className="space-y-5">
          {/* Image upload zone */}
          <Card>
            <h3 className="text-sm font-semibold text-gray-900 mb-3 flex items-center gap-2">
              <Camera className="h-4 w-4 text-gray-500" />
              {t('disease.upload')}
            </h3>

            {!preview ? (
              <div
                onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
                onDragLeave={() => setDragOver(false)}
                onDrop={handleDrop}
                onClick={() => inputRef.current?.click()}
                className={cn(
                  'border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-colors',
                  dragOver
                    ? 'border-primary-400 bg-primary-50'
                    : 'border-gray-200 hover:border-gray-300 hover:bg-gray-50',
                )}
              >
                <Upload className="h-10 w-10 text-gray-300 mx-auto mb-3" />
                <p className="text-sm text-gray-600 font-medium">{t('disease.dragDrop')}</p>
                <p className="text-xs text-gray-400 mt-1">{t('disease.formats')}</p>
                <input
                  ref={inputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) handleFile(f);
                  }}
                />
              </div>
            ) : (
              <div className="relative group">
                <img
                  src={preview}
                  alt="Preview"
                  className="w-full h-64 object-cover rounded-xl border border-gray-100"
                />
                <button
                  onClick={removeFile}
                  className="absolute top-2 right-2 p-1.5 rounded-full bg-black/60 text-white opacity-0 group-hover:opacity-100 transition-opacity"
                  title={t('disease.removeImage')}
                >
                  <X className="h-4 w-4" />
                </button>
                <div className="absolute bottom-2 left-2 px-2 py-1 rounded-lg bg-black/60 text-white text-xs">
                  {file?.name}
                </div>
              </div>
            )}

            {fileError && (
              <div className="mt-3">
                <Alert variant="warning">{fileError}</Alert>
              </div>
            )}
          </Card>

          {/* Crop selector & notes */}
          <Card className="space-y-4">
            <Select
              label={t('disease.selectCrop')}
              value={cropId}
              onChange={(e) => setCropId(e.target.value)}
            >
              <option value="">— None —</option>
              {activeCrops.map((c) => (
                <option key={c.id} value={c.id}>{c.cropName} ({c.season})</option>
              ))}
            </Select>

            <div className="space-y-1.5">
              <label className="block text-sm font-medium text-gray-700">{t('disease.notes')}</label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder={t('disease.notesPlaceholder')}
                rows={3}
                className="w-full rounded-xl border border-gray-300 px-4 py-2.5 text-sm bg-white text-gray-900 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-primary-500 focus:border-primary-500 hover:border-gray-400 transition-colors resize-none"
              />
            </div>
          </Card>

          {/* Submit */}
          <Button
            size="lg"
            className="w-full"
            disabled={!file}
            loading={submitting}
            onClick={handleSubmit}
          >
            <ScanSearch className="h-5 w-5" />
            {submitting ? t('disease.analyzing') : t('disease.analyze')}
          </Button>

          {isVisionAiConfigured() && (
            <p className="text-[11px] text-gray-400 flex items-center justify-center gap-1.5">
              <Sparkles className="h-3 w-3" /> {t('disease.visionActive')}
            </p>
          )}

          {submitError && (
            <Alert variant="error" title={t('disease.analysisFailed')}>{submitError}</Alert>
          )}
        </div>

        {/* Right column: Results */}
        <div className="space-y-5">
          {!result && !leafResult && !aiFailed && !submitting && (
            <Card className="flex flex-col items-center justify-center py-16 text-center">
              <Microscope className="h-16 w-16 text-gray-200 mb-4" />
              <p className="text-sm text-gray-500">Upload an image and click Analyze to see results here.</p>
            </Card>
          )}

          {submitting && (
            <Card className="flex flex-col items-center justify-center py-16 text-center">
              <div className="h-12 w-12 border-4 border-primary-200 border-t-primary-600 rounded-full animate-spin mb-4" />
              <p className="text-sm text-gray-600 font-medium">{t('disease.analyzing')}</p>
              <p className="text-xs text-gray-400 mt-1">This may take up to 2 minutes.</p>
            </Card>
          )}

          {/* Tier 2 failure — Gemini call failed or timed out (graceful notice). */}
          {aiFailed && !submitting && (
            <Alert variant="warning" className="py-8">
              <p className="leading-relaxed">{t('disease.aiUnavailable')}</p>
            </Alert>
          )}

          {/* Leaf guard outcome — rejection card or the diagnostic report. */}
          {leafResult && !submitting && (
            leafResult.isPlantLeaf
              ? <DiagnosticReportCard result={leafResult} />
              : <LeafRejectionCard result={leafResult} />
          )}

          {result && <ResultDisplay result={result} />}
        </div>
      </div>
    </div>
  );
}

/* ─── Result Display ───────────────────────────────────────────────── */
function ResultDisplay({ result }: { result: DiseaseDetectionResponseDto }) {
  // Local reference fallback (AI unavailable): the backend supplies the crop's
  // common diseases plus curated guidance — show that instead of a dead end.
  if (result.isLocalFallback) {
    return <LocalFallbackDisplay result={result} />;
  }

  // Image not accepted
  if (!result.imageAssessment.imageAccepted || !result.imageAssessment.isPlantImage) {
    return (
      <Card>
        <div className="flex items-center gap-3 mb-4">
          <div className="h-10 w-10 rounded-xl bg-amber-50 flex items-center justify-center">
            <AlertTriangle className="h-5 w-5 text-amber-600" />
          </div>
          <div>
            <h3 className="font-semibold text-gray-900">{t('disease.imageAssessment')}</h3>
            <p className="text-xs text-gray-500">Image was not accepted for analysis</p>
          </div>
        </div>
        <p className="text-sm text-gray-700">
          {result.imageAssessment.message || t('disease.notPlant')}
        </p>
        {result.imageAssessment.possiblyBlurry && (
          <p className="text-xs text-amber-600 mt-2 flex items-center gap-1">
            <AlertTriangle className="h-3 w-3" /> The image appears to be blurry.
          </p>
        )}
      </Card>
    );
  }

  const da = result.diseaseAssessment;
  const advice = result.advice;

  return (
    <div className="space-y-4 animate-fade-in">
      {/* Image assessment — compact */}
      <Card padding="sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 text-emerald-500" />
            <span className="text-sm font-medium text-gray-900">{t('disease.imageAssessment')}</span>
          </div>
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <span>{result.imageAssessment.width}×{result.imageAssessment.height}</span>
            {result.imageAssessment.format && (
              <Badge variant="neutral" size="sm">{result.imageAssessment.format.toUpperCase()}</Badge>
            )}
          </div>
        </div>
        {result.imageAssessment.plantConfidence != null && (
          <div className="mt-2 flex items-center gap-2">
            <span className="text-xs text-gray-500">Plant confidence:</span>
            <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
              <div
                className="h-full bg-emerald-500 rounded-full"
                style={{ width: `${(result.imageAssessment.plantConfidence * 100).toFixed(0)}%` }}
              />
            </div>
            <span className="text-xs font-medium text-gray-700">
              {(result.imageAssessment.plantConfidence * 100).toFixed(0)}%
            </span>
          </div>
        )}
      </Card>

      {/* Disease assessment */}
      {da && (
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-3">
            <div className={cn(
              'h-8 w-8 rounded-lg flex items-center justify-center',
              da.detected ? 'bg-red-50' : 'bg-emerald-50',
            )}>
              <Stethoscope className={cn('h-4 w-4', da.detected ? 'text-red-600' : 'text-emerald-600')} />
            </div>
            <div className="flex-1">
              <h3 className="text-sm font-semibold text-gray-900">{t('disease.diseaseAssessment')}</h3>
              <Badge variant={da.detected ? 'danger' : 'success'} size="sm">
                {da.assessmentLevel}
              </Badge>
            </div>
          </div>

          {da.detected && da.disease && (
            <div className="mb-3 p-3 rounded-lg bg-red-50/50 border border-red-100">
              <p className="text-sm font-semibold text-red-800">{da.disease}</p>
              {da.crop && <p className="text-xs text-red-600 mt-0.5">Crop: {da.crop}</p>}
            </div>
          )}

          {!da.detected && (
            <div className="mb-3 p-3 rounded-lg bg-emerald-50/50 border border-emerald-100">
              <p className="text-sm font-semibold text-emerald-800">No disease detected</p>
              {da.explanation && <p className="text-xs text-emerald-600 mt-0.5">{da.explanation}</p>}
            </div>
          )}

          {/* Confidence bar */}
          {da.confidence != null && (
            <div className="space-y-1">
              <div className="flex items-center justify-between text-xs">
                <span className="text-gray-500">{t('disease.confidence')}</span>
                <span className="font-medium text-gray-700">{(da.confidence * 100).toFixed(0)}%</span>
              </div>
              <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                <div
                  className={cn('h-full rounded-full transition-all duration-700', da.detected ? 'bg-red-500' : 'bg-emerald-500')}
                  style={{ width: `${(da.confidence * 100).toFixed(0)}%` }}
                />
              </div>
            </div>
          )}

          {da.severity && (
            <div className="mt-2 flex items-center gap-2 text-xs">
              <span className="text-gray-500">{t('disease.severity')}:</span>
              <Badge variant={da.severity.toLowerCase().includes('high') || da.severity.toLowerCase().includes('severe') ? 'danger' : da.severity.toLowerCase().includes('moderate') ? 'warning' : 'info'} size="sm">
                {da.severity}
              </Badge>
            </div>
          )}

          {da.explanation && da.detected && (
            <p className="text-xs text-gray-600 mt-2 leading-relaxed">{da.explanation}</p>
          )}

          <p className="text-[10px] text-gray-400 mt-2">Source: {da.assessmentSource}</p>
        </Card>
      )}

      {/* Advice */}
      {advice && <AdviceCard advice={advice} />}

      {/* Provider info */}
      <Card padding="sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Info className="h-4 w-4 text-gray-400" />
            <span className="text-xs font-medium text-gray-700">{t('disease.provider')}</span>
          </div>
          <div className="text-xs text-gray-500 text-right">
            <span className="font-medium">{result.provider.name}</span>
            {result.provider.model && <span className="text-gray-400"> · {result.provider.model}</span>}
            {result.provider.version && <span className="text-gray-400"> v{result.provider.version}</span>}
          </div>
        </div>
      </Card>

      {/* Disclaimer */}
      <p className="text-[10px] text-gray-400 text-center leading-relaxed px-4">
        {result.disclaimer || t('disease.disclaimer')}
      </p>

      {/* Crop context info */}
      {result.cropContext && (
        <Card padding="sm" className="bg-gray-50/50">
          <p className="text-xs text-gray-500">
            Analysis context: <span className="font-medium text-gray-700">{result.cropContext.cropName}</span>
            {result.cropContext.season && <span> · {result.cropContext.season}</span>}
            {result.cropContext.growthStage && <span> · {result.cropContext.growthStage}</span>}
            {result.cropContext.plantingDate && <span> · Planted {formatDate(result.cropContext.plantingDate)}</span>}
          </p>
        </Card>
      )}
    </div>
  );
}

/* ─── Gemini Leaf Guard cards ───────────────────────────────────── */

/** Non-plant photo: no report, no pesticides — retake guidance only. */
function LeafRejectionCard({ result }: { result: LeafGuardResult }) {
  return (
    <div className="space-y-3 animate-fade-in">
      <Alert variant="error" title={t('disease.rejectedTitle')}>
        <p className="mt-1 leading-relaxed">{t('disease.rejectedMessage')}</p>
      </Alert>

      {result.rejectionReason && (
        <Card padding="sm">
          <p className="text-xs text-gray-600 leading-relaxed">
            <span className="font-semibold text-gray-800">{t('disease.rejectionReason')}: </span>
            {result.rejectionReason}
          </p>
        </Card>
      )}

      <p className="text-xs text-gray-400 text-center">{t('disease.retakeHint')}</p>
    </div>
  );
}

/** Plant leaf confirmed — the structured diagnostic report. */
function DiagnosticReportCard({ result }: { result: LeafGuardResult }) {
  return (
    <Card className="animate-fade-in">
      <div className="flex items-center gap-3 mb-4">
        <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-emerald-500 to-green-600 flex items-center justify-center shrink-0">
          <Leaf className="h-5 w-5 text-white" />
        </div>
        <div>
          <h3 className="font-semibold text-gray-900">{t('disease.report.title')}</h3>
          <p className="text-xs text-emerald-600 flex items-center gap-1 mt-0.5">
            <CheckCircle2 className="h-3 w-3" /> {result.servedBy ?? t('disease.visionActive')}
          </p>
        </div>
      </div>

      <div className="space-y-3">
        <ReportRow
          icon="🌿"
          label={t('disease.report.healthStatus')}
          value={result.diseaseName ?? t('disease.report.noDisease')}
          highlight
        />
        {result.severity && (
          <ReportRow icon="📊" label={t('disease.report.severity')} value={result.severity} />
        )}
        {result.pesticide && (
          <ReportRow icon="🧪" label={t('disease.report.chemical')} value={result.pesticide} />
        )}
        {result.dosagePerAcre && (
          <ReportRow icon="💧" label={t('disease.report.dosage')} value={result.dosagePerAcre} />
        )}
        {result.prevention && (
          <ReportRow icon="🛡️" label={t('disease.report.prevention')} value={result.prevention} />
        )}
      </div>

      <p className="text-[10px] text-gray-400 text-center leading-relaxed mt-4">
        {t('disease.disclaimer')}
      </p>
    </Card>
  );
}

function ReportRow({ icon, label, value, highlight }: {
  icon: string;
  label: string;
  value: string;
  highlight?: boolean;
}) {
  return (
    <div className={cn(
      'flex items-start gap-3 rounded-xl border p-3',
      highlight ? 'border-emerald-200 bg-emerald-50/50' : 'border-gray-100 bg-gray-50/50',
    )}>
      <span className="text-lg leading-none mt-0.5 shrink-0">{icon}</span>
      <div className="min-w-0">
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500">{label}</p>
        <p className={cn('text-sm mt-0.5 leading-relaxed break-words', highlight ? 'font-semibold text-gray-900' : 'text-gray-700')}>
          {value}
        </p>
      </div>
    </div>
  );
}

/* ─── Advice card (shared by AI result and local fallback) ─────────── */
function AdviceCard({ advice }: { advice: DiseaseAdviceDto }) {
  return (
    <Card padding="sm">
      <div className="flex items-center gap-2 mb-3">
        <div className="h-8 w-8 rounded-lg bg-primary-50 flex items-center justify-center">
          <Leaf className="h-4 w-4 text-primary-600" />
        </div>
        <h3 className="text-sm font-semibold text-gray-900">{t('disease.advice')}</h3>
      </div>

      <p className="text-sm text-gray-700 mb-3 leading-relaxed">{advice.summary}</p>

      {advice.recommendedActions.length > 0 && (
        <div className="mb-3">
          <h4 className="text-xs font-semibold text-gray-700 mb-1.5 flex items-center gap-1">
            <ShieldCheck className="h-3 w-3" /> {t('disease.recommendedActions')}
          </h4>
          <ul className="space-y-1">
            {advice.recommendedActions.map((a, i) => (
              <li key={i} className="text-xs text-gray-600 flex items-start gap-2">
                <span className="h-1.5 w-1.5 rounded-full bg-primary-500 mt-1.5 shrink-0" />
                {a}
              </li>
            ))}
          </ul>
        </div>
      )}

      {advice.prevention.length > 0 && (
        <div className="mb-3">
          <h4 className="text-xs font-semibold text-gray-700 mb-1.5 flex items-center gap-1">
            <ShieldCheck className="h-3 w-3" /> {t('disease.prevention')}
          </h4>
          <ul className="space-y-1">
            {advice.prevention.map((a, i) => (
              <li key={i} className="text-xs text-gray-600 flex items-start gap-2">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 mt-1.5 shrink-0" />
                {a}
              </li>
            ))}
          </ul>
        </div>
      )}

      {advice.monitoring.length > 0 && (
        <div className="mb-3">
          <h4 className="text-xs font-semibold text-gray-700 mb-1.5 flex items-center gap-1">
            <Activity className="h-3 w-3" /> {t('disease.monitoring')}
          </h4>
          <ul className="space-y-1">
            {advice.monitoring.map((a, i) => (
              <li key={i} className="text-xs text-gray-600 flex items-start gap-2">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500 mt-1.5 shrink-0" />
                {a}
              </li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  );
}

/* ─── Local Reference fallback (AI unavailable) ───────────────────── */
function LocalFallbackDisplay({ result }: { result: DiseaseDetectionResponseDto }) {
  const da = result.diseaseAssessment;
  const advice = result.advice;

  return (
    <div className="space-y-4 animate-fade-in">
      {/* Mode banner */}
      <Alert variant="warning" title={t('disease.localModeTitle')}>
        <p className="mt-1">{t('disease.localModeDescription')}</p>
      </Alert>

      {/* Image received confirmation */}
      <Card padding="sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 text-emerald-500" />
            <span className="text-sm font-medium text-gray-900">{t('disease.imageAssessment')}</span>
          </div>
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <span>{result.imageAssessment.width}×{result.imageAssessment.height}</span>
            {result.imageAssessment.format && (
              <Badge variant="neutral" size="sm">{result.imageAssessment.format.toUpperCase()}</Badge>
            )}
          </div>
        </div>
      </Card>

      {/* Common diseases for the crop */}
      {da && (
        <Card padding="sm">
          <div className="flex items-center gap-2 mb-3">
            <div className="h-8 w-8 rounded-lg bg-amber-50 flex items-center justify-center">
              <Stethoscope className="h-4 w-4 text-amber-600" />
            </div>
            <h3 className="text-sm font-semibold text-gray-900">{t('disease.commonDiseases')}</h3>
          </div>

          {da.commonDiseasesForCrop.length > 0 && (
            <div className="flex flex-wrap gap-2 mb-3">
              {da.commonDiseasesForCrop.map((d) => (
                <Badge key={d} variant="warning" size="sm">{d}</Badge>
              ))}
            </div>
          )}

          {da.explanation && (
            <p className="text-xs text-gray-600 leading-relaxed">{da.explanation}</p>
          )}
        </Card>
      )}

      {/* Guidance */}
      {advice && <AdviceCard advice={advice} />}

      {/* Missing data notes */}
      {result.missingData.length > 0 && (
        <Card padding="sm" className="bg-gray-50/50">
          <ul className="space-y-1.5">
            {result.missingData.map((m, i) => (
              <li key={i} className="text-xs text-gray-500 flex items-start gap-2">
                <Info className="h-3 w-3 mt-0.5 shrink-0" />
                {m}
              </li>
            ))}
          </ul>
        </Card>
      )}

      {/* Provider info */}
      <Card padding="sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Info className="h-4 w-4 text-gray-400" />
            <span className="text-xs font-medium text-gray-700">{t('disease.provider')}</span>
          </div>
          <div className="text-xs text-gray-500 text-right">
            <span className="font-medium">{result.provider.name}</span>
          </div>
        </div>
      </Card>

      {/* Disclaimer + hint */}
      <p className="text-[10px] text-gray-400 text-center leading-relaxed px-4">
        {result.disclaimer || t('disease.disclaimer')}
      </p>
      <p className="text-[10px] text-gray-400 text-center leading-relaxed px-4">
        {t('disease.localModeHint')}
      </p>
    </div>
  );
}
