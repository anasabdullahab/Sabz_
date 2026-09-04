import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { farmApi } from '@/api/farmApi';
import { cropApi } from '@/api/cropApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Modal } from '@/components/ui/Modal';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/EmptyState';
import { formatDate, formatUnit } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  ArrowLeft, MapPin, Layers, Droplets, Maximize, Clock, Edit, Trash2,
  Cloud, Sprout, Calendar, ScanSearch,
  DollarSign, LayoutDashboard, Bot, Calculator,
} from 'lucide-react';
import type { FarmResponseDto, CropResponseDto } from '@/types';

export function FarmDetailPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();
  const [farm, setFarm] = useState<FarmResponseDto | null>(null);
  const [crops, setCrops] = useState<CropResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!farmId) return;
    Promise.all([farmApi.getById(farmId), cropApi.getByFarm(farmId)])
      .then(([f, c]) => { setFarm(f); setCrops(c); })
      .catch((err) => setError(parseApiError(err).message))
      .finally(() => setLoading(false));
  }, [farmId]);

  const handleDelete = async () => {
    if (!farm) return;
    setDeleting(true);
    try {
      await farmApi.delete(farm.id);
      navigate('/farms');
    } catch {
      setDeleteOpen(false);
    } finally {
      setDeleting(false);
    }
  };

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} />;
  if (!farm) return <ErrorState message={t('farm.notFound')} />;

  const activeCrops = crops.filter((c) => c.status?.toLowerCase() !== 'harvested');
  const historyCrops = crops.filter((c) => c.status?.toLowerCase() === 'harvested');

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Back + actions */}
      <div className="flex items-center justify-between">
        <button
          onClick={() => navigate('/farms')}
          className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('common.back')}
        </button>
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" onClick={() => navigate(`/farms/${farm.id}/edit`)}>
            <Edit className="h-4 w-4" /> {t('common.edit')}
          </Button>
          <Button variant="danger" size="sm" onClick={() => setDeleteOpen(true)}>
            <Trash2 className="h-4 w-4" /> {t('common.delete')}
          </Button>
        </div>
      </div>

      {/* Farm header */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="h-2 bg-gradient-to-r from-primary-500 via-primary-600 to-emerald-500" />
        <div className="p-6 lg:p-8">
          <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 mb-2">{farm.farmName}</h1>
          <div className="flex flex-wrap items-center gap-4 text-sm text-gray-500">
            <span className="flex items-center gap-1.5">
              <MapPin className="h-4 w-4" />
              {[farm.tehsilName, farm.districtName, farm.provinceName].filter(Boolean).join(', ')}
            </span>
            <span className="flex items-center gap-1.5">
              <Maximize className="h-4 w-4" />
              {formatUnit(farm.farmSize, farm.farmSizeUnit)}
            </span>
            {farm.soilType && (
              <span className="flex items-center gap-1.5">
                <Layers className="h-4 w-4" /> {farm.soilType}
              </span>
            )}
            {farm.irrigationType && (
              <span className="flex items-center gap-1.5">
                <Droplets className="h-4 w-4" /> {farm.irrigationType}
              </span>
            )}
          </div>
          {farm.latitude && farm.longitude && (
            <p className="text-xs text-gray-400 mt-2">
              GPS: {farm.latitude}, {farm.longitude}
            </p>
          )}
          <div className="flex gap-2 mt-3 text-xs text-gray-400">
            <span className="flex items-center gap-1">
              <Clock className="h-3 w-3" /> Created {formatDate(farm.createdAt)}
            </span>
            {farm.updatedAt && (
              <span>Updated {formatDate(farm.updatedAt)}</span>
            )}
          </div>
        </div>
      </div>

      {/* Quick navigation cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
        {[
          { icon: LayoutDashboard, label: t('farmDetail.dashboard'), desc: t('farmDetail.dashboardDesc'), path: 'dashboard', color: 'from-indigo-500 to-indigo-600' },
          { icon: Cloud, label: t('farmDetail.weather'), desc: t('farmDetail.weatherDesc'), path: 'weather', color: 'from-sky-500 to-sky-600' },
          { icon: Sprout, label: t('farmDetail.crops'), desc: `${crops.length} ${t('farmDetail.cropsDesc')}`, path: 'crops', color: 'from-primary-500 to-primary-600' },
          { icon: ScanSearch, label: t('farmDetail.diseaseDetection'), desc: t('farmDetail.diseaseDesc'), path: 'disease-detection', color: 'from-rose-500 to-orange-500' },
          { icon: DollarSign, label: t('farmDetail.financial'), desc: t('farmDetail.financialDesc'), path: 'financial', color: 'from-emerald-500 to-teal-600' },
          { icon: Bot, label: t('farmDetail.agronomist'), desc: t('farmDetail.agronomistDesc'), path: 'agronomist', color: 'from-cyan-500 to-blue-600' },
          { icon: Calculator, label: t('farmDetail.calculator'), desc: t('farmDetail.calculatorDesc'), path: 'input-calculator', color: 'from-blue-500 to-indigo-600', global: true },
        ].map(({ icon: Icon, label, desc, path, color, global }) => (
          <Card key={path} hover onClick={() => navigate(global ? `/${path}` : `/farms/${farm.id}/${path}`)} padding="none">
            <div className={`h-1 bg-gradient-to-r ${color} rounded-t-2xl`} />
            <div className="p-4 flex items-center gap-3">
              <div className={`h-10 w-10 rounded-xl bg-gradient-to-br ${color} flex items-center justify-center shrink-0`}>
                <Icon className="h-5 w-5 text-white" />
              </div>
              <div>
                <h3 className="font-semibold text-sm text-gray-900">{label}</h3>
                <p className="text-xs text-gray-500">{desc}</p>
              </div>
            </div>
          </Card>
        ))}
      </div>

      {/* Active Crops */}
      <div>
        <h2 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2 flex-wrap">
          <Sprout className="h-5 w-5 text-primary-600" />
          {t('farmDetail.activeCrops')}
          <Badge variant="primary">{activeCrops.length}</Badge>
        </h2>
        {activeCrops.length === 0 ? (
          <Card>
            <p className="text-sm text-gray-500 text-center py-4">
              {t('crop.noCrops')}{' '}
              <button
                onClick={() => navigate(`/farms/${farm.id}/crops/new`)}
                className="text-primary-700 font-medium hover:underline"
              >
                {t('farmDetail.addCrop')}
              </button>
            </p>
          </Card>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {activeCrops.map((crop) => (
              <CropRow key={crop.id} crop={crop} />
            ))}
          </div>
        )}
      </div>

      {/* Crop History */}
      {historyCrops.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2">
            <Calendar className="h-5 w-5 text-gray-400" />
            {t('crop.history')}
          </h2>
          <div className="space-y-2">
            {historyCrops.map((crop) => (
              <CropRow key={crop.id} crop={crop} />
            ))}
          </div>
        </div>
      )}

      {/* Delete modal */}
      <Modal open={deleteOpen} onClose={() => setDeleteOpen(false)} title={t('farmDetail.deleteFarm')}>
        <p className="text-sm text-gray-600 mb-6">{t('farm.deleteConfirm')}</p>
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={() => setDeleteOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="danger" loading={deleting} onClick={handleDelete}>{t('common.delete')}</Button>
        </div>
      </Modal>
    </div>
  );
}

function CropRow({ crop }: { crop: CropResponseDto }) {
  const navigate = useNavigate();
  const statusVariant =
    crop.status?.toLowerCase() === 'harvested' ? 'neutral' :
    crop.status?.toLowerCase() === 'active' ? 'success' :
    'info';

  return (
    <div
      onClick={() => navigate(`/farms/${crop.farmId}/crops`)}
      className="flex items-center justify-between bg-white rounded-xl border border-gray-100 p-4 hover:shadow-sm cursor-pointer transition-shadow"
    >
      <div>
        <h4 className="font-medium text-gray-900">{crop.cropName}</h4>
        <div className="flex items-center gap-2 mt-0.5 text-xs text-gray-500">
          <span>{crop.season}</span>
          {crop.plantingDate && <span>{t('farmDetail.planted')} {formatDate(crop.plantingDate)}</span>}
        </div>
      </div>
      <Badge variant={statusVariant}>{crop.status || 'Unknown'}</Badge>
    </div>
  );
}
