import { useEffect, useState } from 'react';
import { farmApi } from '@/api/farmApi';
import { cropApi } from '@/api/cropApi';
import { inputCalculatorApi } from '@/api/inputCalculatorApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { t } from '@/lib/i18n';
import { Calculator, Sprout, MapPin, ArrowRight, AlertTriangle } from 'lucide-react';
import type { FarmResponseDto, CropResponseDto, InputCalculatorRequestDto, InputCalculatorResponseDto } from '@/types';

const emptyForm: InputCalculatorRequestDto = {
  cropId: null,
  inputName: '',
  category: '',
  dosageRate: 0,
  dosageUnit: '',
  dosageBasis: '',
};

export function InputCalculatorPage() {
  const [farms, setFarms] = useState<FarmResponseDto[]>([]);
  const [crops, setCrops] = useState<CropResponseDto[]>([]);
  const [selectedFarmId, setSelectedFarmId] = useState('');
  const [form, setForm] = useState<InputCalculatorRequestDto>({ ...emptyForm });
  const [result, setResult] = useState<InputCalculatorResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [calculating, setCalculating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    farmApi.getAll()
      .then(setFarms)
      .catch((err) => setError(parseApiError(err).message))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!selectedFarmId) { setCrops([]); return; }
    cropApi.getByFarm(selectedFarmId).then(setCrops).catch(() => {});
  }, [selectedFarmId]);

  const handleCalculate = async () => {
    if (!selectedFarmId) return;
    setCalculating(true);
    setError(null);
    try {
      const data = await inputCalculatorApi.calculate(selectedFarmId, form);
      setResult(data);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setCalculating(false);
    }
  };

  if (loading) return <PageSkeleton />;

  return (
    <div className="space-y-6 animate-fade-in max-w-3xl mx-auto">
      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center">
            <Calculator className="h-5 w-5 text-white" />
          </div>
          {t('calculator.title')}
        </h1>
        <p className="text-gray-500 mt-1 ml-[52px]">{t('calculator.description')}</p>
      </div>

      {/* Farm & Crop Selectors */}
      <Card padding="sm">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-medium text-gray-700 mb-1 block">
              <MapPin className="h-3 w-3 inline mr-1" />{t('farm.name')} *
            </label>
            <select
              value={selectedFarmId}
              onChange={(e) => { setSelectedFarmId(e.target.value); setForm((f) => ({ ...f, cropId: null })); setResult(null); }}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">-- Select Farm --</option>
              {farms.map((f) => (
                <option key={f.id} value={f.id}>{f.farmName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="text-xs font-medium text-gray-700 mb-1 block">
              <Sprout className="h-3 w-3 inline mr-1" />{t('crop.name')} (optional)
            </label>
            <select
              value={form.cropId || ''}
              onChange={(e) => setForm((f) => ({ ...f, cropId: e.target.value || null }))}
              disabled={!selectedFarmId}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500 disabled:opacity-50"
            >
              <option value="">-- {t('financial.allCrops')} --</option>
              {crops.map((c) => (
                <option key={c.id} value={c.id}>{c.cropName} ({c.season})</option>
              ))}
            </select>
          </div>
        </div>
      </Card>

      {/* Input Form */}
      {selectedFarmId && (
        <Card padding="md">
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-xs font-medium text-gray-700 mb-1 block">{t('calculator.inputName')} *</label>
                <input
                  value={form.inputName}
                  onChange={(e) => setForm((f) => ({ ...f, inputName: e.target.value }))}
                  placeholder={t('calculator.inputNamePlaceholder')}
                  className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-gray-700 mb-1 block">{t('calculator.category')} *</label>
                <input
                  value={form.category}
                  onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))}
                  placeholder={t('calculator.categoryPlaceholder')}
                  className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div>
                <label className="text-xs font-medium text-gray-700 mb-1 block">{t('calculator.dosageRate')} *</label>
                <input
                  type="number"
                  value={form.dosageRate || ''}
                  onChange={(e) => setForm((f) => ({ ...f, dosageRate: Number(e.target.value) }))}
                  min={0}
                  step="any"
                  className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-gray-700 mb-1 block">{t('calculator.dosageUnit')} *</label>
                <input
                  value={form.dosageUnit}
                  onChange={(e) => setForm((f) => ({ ...f, dosageUnit: e.target.value }))}
                  placeholder={t('calculator.dosageUnitPlaceholder')}
                  className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-gray-700 mb-1 block">{t('calculator.dosageBasis')} *</label>
                <input
                  value={form.dosageBasis}
                  onChange={(e) => setForm((f) => ({ ...f, dosageBasis: e.target.value }))}
                  placeholder={t('calculator.dosageBasisPlaceholder')}
                  className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
            </div>

            {error && <p className="text-sm text-red-600">{error}</p>}

            <div className="flex justify-end pt-2">
              <Button
                variant="primary"
                loading={calculating}
                disabled={!form.inputName || !form.category || !form.dosageRate || !form.dosageUnit || !form.dosageBasis}
                onClick={handleCalculate}
              >
                <Calculator className="h-4 w-4" /> {t('calculator.calculate')}
              </Button>
            </div>
          </div>
        </Card>
      )}

      {/* Result */}
      {result && (
        <Card padding="md">
          <div className="flex items-center gap-2 mb-4">
            <ArrowRight className="h-4 w-4 text-primary-600" />
            <h3 className="font-semibold text-gray-900">{t('calculator.result')}</h3>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            <ResultItem label={t('calculator.inputName')} value={result.inputName} />
            <ResultItem label={t('calculator.category')} value={result.category} />
            <ResultItem label={t('calculator.farmArea')} value={`${result.farmArea} ${result.farmAreaUnit}`} />
            <ResultItem label={t('calculator.calculationArea')} value={`${result.calculationArea} ${result.calculationAreaUnit}`} />
            <ResultItem label={t('calculator.dosageRate')} value={`${result.dosageRate} ${result.dosageUnit}/${result.dosageBasis}`} />
            <div className="bg-primary-50 rounded-xl p-3">
              <p className="text-[10px] font-medium text-primary-600 uppercase tracking-wider">{t('calculator.requiredQuantity')}</p>
              <p className="text-lg font-bold text-primary-800">{result.requiredQuantity} {result.requiredQuantityUnit}</p>
            </div>
          </div>

          <div className="mt-4 pt-4 border-t border-gray-100 space-y-2">
            <p className="text-xs text-gray-500">
              <span className="font-medium">{t('calculator.formula')}:</span> {result.calculationFormula}
            </p>
            {result.conversionApplied && (
              <p className="text-xs text-amber-600 flex items-center gap-1">
                <AlertTriangle className="h-3 w-3" /> {t('calculator.conversionApplied')}
              </p>
            )}
            <p className="text-[10px] text-gray-400">{result.disclaimer}</p>
          </div>
        </Card>
      )}

      {!selectedFarmId && (
        <EmptyState
          icon={<Calculator className="h-16 w-16" />}
          title={t('calculator.noResult')}
        />
      )}
    </div>
  );
}

function ResultItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider">{label}</p>
      <p className="text-sm font-semibold text-gray-900">{value}</p>
    </div>
  );
}
