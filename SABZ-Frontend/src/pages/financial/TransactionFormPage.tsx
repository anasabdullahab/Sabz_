import { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { financialApi } from '@/api/financialApi';
import { cropApi } from '@/api/cropApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Alert } from '@/components/ui/Alert';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/EmptyState';
import {
  EXPENSE_CATEGORIES,
  INCOME_CATEGORIES,
  QUICK_EXPENSE_CATEGORIES,
  QUICK_INCOME_CATEGORIES,
  categoryLabel,
} from '@/lib/financialCategories';
import { cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { ArrowLeft, DollarSign, Save } from 'lucide-react';
import type { CropResponseDto, CreateFinancialTransactionDto } from '@/types';

const MAX_AMOUNT = 1_000_000_000;

export function TransactionFormPage() {
  const { farmId, transactionId } = useParams<{ farmId: string; transactionId?: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const isEdit = !!transactionId;

  const [crops, setCrops] = useState<CropResponseDto[]>([]);
  const [loading, setLoading] = useState(isEdit);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Form fields (type preset via ?type= from the ledger's 2-button log)
  const [transactionType, setTransactionType] = useState(
    searchParams.get('type') === 'Income' ? 'Income' : 'Expense',
  );
  const [category, setCategory] = useState('');
  const [amount, setAmount] = useState('');
  const [transactionDate, setTransactionDate] = useState(new Date().toISOString().slice(0, 10));
  const [cropId, setCropId] = useState('');
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    if (!farmId) return;
    cropApi.getByFarm(farmId)
      .then(setCrops)
      .catch((err) => setLoadError(parseApiError(err).message));
  }, [farmId]);

  useEffect(() => {
    if (!isEdit || !transactionId) return;
    setLoading(true);
    financialApi.getById(transactionId)
      .then((tx) => {
        setTransactionType(tx.transactionType);
        setCategory(tx.category);
        setAmount(String(tx.amount));
        setTransactionDate(tx.transactionDate.slice(0, 10));
        setCropId(tx.cropId || '');
        setNotes(tx.notes || '');
      })
      .catch((err) => setLoadError(parseApiError(err).message))
      .finally(() => setLoading(false));
  }, [isEdit, transactionId]);

  const categoryOptions = transactionType === 'Income' ? INCOME_CATEGORIES : EXPENSE_CATEGORIES;

  const validate = (): boolean => {
    const errs: Record<string, string> = {};
    if (!category) errs.category = 'Please select a category.';
    const numAmount = parseFloat(amount);
    if (!amount || isNaN(numAmount) || numAmount <= 0) errs.amount = 'Enter a valid positive amount.';
    else if (numAmount > MAX_AMOUNT) errs.amount = t('financial.maxAmount');
    if (!transactionDate) errs.transactionDate = 'Please select a date.';
    else {
      const today = new Date().toISOString().slice(0, 10);
      if (transactionDate > today) errs.transactionDate = t('financial.noFutureDate');
    }
    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate() || !farmId) return;
    setSubmitting(true);
    setSubmitError(null);

    const dto: CreateFinancialTransactionDto = {
      transactionType,
      category,
      amount: parseFloat(amount),
      transactionDate: transactionDate || null,
      cropId: cropId || null,
      notes: notes || null,
    };

    try {
      if (isEdit && transactionId) {
        await financialApi.update(transactionId, dto);
      } else {
        await financialApi.create(farmId, dto);
      }
      navigate(`/farms/${farmId}/financial`);
    } catch (err) {
      setSubmitError(parseApiError(err).message);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <PageSkeleton />;
  if (loadError) return <ErrorState message={loadError} />;

  return (
    <div className="space-y-6 animate-fade-in max-w-2xl">
      {/* Back */}
      <button
        onClick={() => navigate(`/farms/${farmId}/financial`)}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      {/* Header */}
      <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
        <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center">
          <DollarSign className="h-5 w-5 text-white" />
        </div>
        {isEdit ? t('financial.editTransaction') : transactionType === 'Income' ? t('financial.addIncome') : t('financial.addExpense')}
      </h1>

      <Card>
        <div className="space-y-5">
          {/* Type */}
          <Select
            label={t('financial.type')}
            value={transactionType}
            onChange={(e) => {
              setTransactionType(e.target.value);
              setCategory(''); // reset category when type changes
            }}
          >
            <option value="Income">Income</option>
            <option value="Expense">Expense</option>
          </Select>

          {/* Quick category presets */}
          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-gray-700">{t('financial.quickCategories')}</label>
            <div className="flex flex-wrap gap-2">
              {(transactionType === 'Income' ? QUICK_INCOME_CATEGORIES : QUICK_EXPENSE_CATEGORIES).map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => setCategory(c)}
                  className={cn(
                    'px-3 py-1.5 rounded-full text-xs font-medium border transition-colors',
                    category === c
                      ? 'bg-primary-700 text-white border-primary-700'
                      : 'bg-white text-gray-600 border-gray-200 hover:border-primary-300 hover:text-primary-700',
                  )}
                >
                  {categoryLabel(c)}
                </button>
              ))}
            </div>
          </div>

          {/* Category */}
          <Select
            label={t('financial.category')}
            value={category}
            error={errors.category}
            onChange={(e) => setCategory(e.target.value)}
            placeholder="Select category..."
          >
            {categoryOptions.map((c) => (
              <option key={c} value={c}>{categoryLabel(c)}</option>
            ))}
          </Select>

          {/* Amount */}
          <Input
            label={`${t('financial.amount')} (PKR)`}
            type="number"
            min="0.01"
            step="0.01"
            max={String(MAX_AMOUNT)}
            value={amount}
            error={errors.amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0.00"
          />

          {/* Date */}
          <Input
            label={t('financial.date')}
            type="date"
            value={transactionDate}
            max={new Date().toISOString().slice(0, 10)}
            error={errors.transactionDate}
            onChange={(e) => setTransactionDate(e.target.value)}
          />

          {/* Crop (optional) */}
          <Select
            label={t('financial.crop')}
            value={cropId}
            onChange={(e) => setCropId(e.target.value)}
          >
            <option value="">— None (farm-level) —</option>
            {crops.map((c) => (
              <option key={c.id} value={c.id}>{c.cropName} ({c.season})</option>
            ))}
          </Select>

          {/* Notes */}
          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-gray-700">{t('financial.notes')}</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder={t('financial.notesPlaceholder')}
              rows={3}
              className="w-full rounded-xl border border-gray-300 px-4 py-2.5 text-sm bg-white text-gray-900 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-primary-500 hover:border-gray-400 transition-colors resize-none"
            />
          </div>
        </div>
      </Card>

      {submitError && (
        <Alert variant="error" title="Error">{submitError}</Alert>
      )}

      {/* Submit */}
      <div className="flex gap-3">
        <Button
          variant="secondary"
          onClick={() => navigate(`/farms/${farmId}/financial`)}
        >
          {t('common.cancel')}
        </Button>
        <Button
          size="lg"
          className="flex-1"
          loading={submitting}
          onClick={handleSubmit}
        >
          <Save className="h-4 w-4" />
          {submitting ? t('common.saving') : t('common.save')}
        </Button>
      </div>
    </div>
  );
}
