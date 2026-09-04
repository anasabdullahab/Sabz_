import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { cropApi } from '@/api/cropApi';
import { cropKnowledgeApi } from '@/api/cropKnowledgeApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Modal } from '@/components/ui/Modal';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { CropStageProgress } from '@/components/crops/CropStageProgress';
import { findKnowledgeEntry, getStageProgress } from '@/lib/cropStages';
import { formatDate } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { Plus, ArrowLeft, Edit, Trash2, Sprout, Calendar, ClipboardCheck, DollarSign } from 'lucide-react';
import type { CropKnowledgeEntryDto, CropResponseDto } from '@/types';

export function CropsPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();
  const [crops, setCrops] = useState<CropResponseDto[]>([]);
  const [knowledge, setKnowledge] = useState<CropKnowledgeEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<CropResponseDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  const load = async () => {
    if (!farmId) return;
    setLoading(true);
    try { setCrops(await cropApi.getByFarm(farmId)); }
    catch (err) { setError(parseApiError(err).message); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(); }, [farmId]);

  // Knowledge base powers the stage progress bars on active crop cards
  // (progressive enhancement — failures are ignored)
  useEffect(() => {
    cropKnowledgeApi.getCrops().then(setKnowledge).catch(() => { /* stage bars are optional */ });
  }, []);

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try { await cropApi.delete(deleteTarget.id); setCrops((prev) => prev.filter((c) => c.id !== deleteTarget.id)); }
    finally { setDeleting(false); setDeleteTarget(null); }
  };

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={load} />;

  const activeCrops = crops.filter((c) => c.status?.toLowerCase() !== 'harvested');
  const historyCrops = crops.filter((c) => c.status?.toLowerCase() === 'harvested');

  return (
    <div className="space-y-6 animate-fade-in">
      <button onClick={() => navigate(`/farms/${farmId}`)} className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors">
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Crops</h1>
          <p className="text-gray-500 text-sm mt-0.5">{crops.length} crop records</p>
        </div>
        <Button onClick={() => navigate(`/farms/${farmId}/crops/new`)}>
          <Plus className="h-4 w-4" /> {t('crop.add')}
        </Button>
      </div>

      {crops.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Sprout className="h-16 w-16" />}
            title={t('crop.noCrops')}
            description="Add your first crop record to start tracking your growing season."
            action={{ label: t('crop.add'), onClick: () => navigate(`/farms/${farmId}/crops/new`) }}
          />
        </Card>
      ) : (
        <>
          {/* Active crops */}
          {activeCrops.length > 0 && (
            <div className="space-y-3">
              <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wider">Active Crops</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {activeCrops.map((crop) => (
                  <CropCard key={crop.id} crop={crop} farmId={farmId!} knowledge={knowledge} onEdit={() => navigate(`/farms/${farmId}/crops/${crop.id}/edit`)} onDelete={() => setDeleteTarget(crop)} />
                ))}
              </div>
            </div>
          )}

          {/* History */}
          {historyCrops.length > 0 && (
            <div className="space-y-3">
              <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wider flex items-center gap-2">
                <Calendar className="h-4 w-4" /> {t('crop.history')}
              </h2>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {historyCrops.map((crop) => (
                  <CropCard key={crop.id} crop={crop} farmId={farmId!} knowledge={knowledge} onEdit={() => navigate(`/farms/${farmId}/crops/${crop.id}/edit`)} onDelete={() => setDeleteTarget(crop)} />
                ))}
              </div>
            </div>
          )}
        </>
      )}

      <Modal open={!!deleteTarget} onClose={() => setDeleteTarget(null)} title="Delete Crop">
        <p className="text-sm text-gray-600 mb-6">{t('crop.deleteConfirm')}</p>
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={() => setDeleteTarget(null)}>{t('common.cancel')}</Button>
          <Button variant="danger" loading={deleting} onClick={handleDelete}>{t('common.delete')}</Button>
        </div>
      </Modal>
    </div>
  );
}

function CropCard({ crop, farmId, knowledge, onEdit, onDelete }: { crop: CropResponseDto; farmId: string; knowledge: CropKnowledgeEntryDto[]; onEdit: () => void; onDelete: () => void }) {
  const navigate = useNavigate();
  const isHarvested = crop.status?.toLowerCase() === 'harvested';
  const statusVariant =
    isHarvested ? 'neutral' :
    crop.status?.toLowerCase() === 'active' ? 'success' : 'info';

  // Visual growth-stage timeline for growing crops (knowledge base + planting date)
  const stageProgress = isHarvested
    ? null
    : getStageProgress(findKnowledgeEntry(crop.cropName, knowledge), crop.plantingDate);

  return (
    <Card padding="sm" className="space-y-3">
      <div className="flex items-start justify-between">
        <div>
          <h3 className="font-semibold text-gray-900">{crop.cropName}</h3>
          <div className="flex items-center gap-2 mt-0.5">
            <Badge variant="primary" size="sm">{crop.season}</Badge>
            <Badge variant={statusVariant} size="sm">{crop.status || '—'}</Badge>
          </div>
        </div>
        <div className="flex gap-1">
          <button onClick={onEdit} className="p-1.5 rounded-lg text-gray-400 hover:text-primary-700 hover:bg-primary-50 transition-colors" aria-label="Edit">
            <Edit className="h-4 w-4" />
          </button>
          <button onClick={onDelete} className="p-1.5 rounded-lg text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors" aria-label="Delete">
            <Trash2 className="h-4 w-4" />
          </button>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-2 text-xs text-gray-500">
        {crop.plantingDate && <span>Planted: {formatDate(crop.plantingDate)}</span>}
        {crop.harvestDate && <span>Harvest: {formatDate(crop.harvestDate)}</span>}
        {crop.growthStage && <span>Stage: {crop.growthStage}</span>}
        {crop.previousCrop && <span>Previous: {crop.previousCrop}</span>}
      </div>
      {stageProgress && <CropStageProgress progress={stageProgress} />}
      <div className="flex gap-2 pt-2 border-t border-gray-100">
        <button
          onClick={() => navigate(`/farms/${farmId}/crops/${crop.id}/monitoring`)}
          className="flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg text-xs font-medium text-gray-500 hover:text-primary-700 hover:bg-primary-50 transition-colors"
        >
          <ClipboardCheck className="h-3.5 w-3.5" /> Monitoring
        </button>
        <button
          onClick={() => navigate(`/farms/${farmId}/crops/${crop.id}/financial-health`)}
          className="flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg text-xs font-medium text-gray-500 hover:text-emerald-700 hover:bg-emerald-50 transition-colors"
        >
          <DollarSign className="h-3.5 w-3.5" /> Financial
        </button>
      </div>
    </Card>
  );
}
