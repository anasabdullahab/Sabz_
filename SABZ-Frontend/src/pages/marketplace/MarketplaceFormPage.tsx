import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { marketplaceApi } from '@/api/marketplaceApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { t } from '@/lib/i18n';
import { ArrowLeft, Store } from 'lucide-react';
import type { CreateMarketplaceListingDto } from '@/types';

const empty: CreateMarketplaceListingDto = {
  title: '', category: '', listingType: 'Sale', description: '', price: 0,
  priceUnit: 'Total', location: '', contactNumber: '', condition: 'New',
  availability: '', imageUrl: null,
};

export function MarketplaceFormPage() {
  const { listingId } = useParams<{ listingId: string }>();
  const navigate = useNavigate();
  const isEdit = !!listingId;
  const [form, setForm] = useState<CreateMarketplaceListingDto>({ ...empty });
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!isEdit || !listingId) return;
    setLoading(true);
    marketplaceApi.getListing(listingId)
      .then((d) => setForm({
        title: d.title, category: d.category, listingType: d.listingType,
        description: d.description, price: d.price, priceUnit: d.priceUnit,
        location: d.location, contactNumber: d.contactNumber || '',
        condition: d.condition, availability: d.availability, imageUrl: d.imageUrl,
      }))
      .catch((err) => setError(parseApiError(err).message))
      .finally(() => setLoading(false));
  }, [listingId, isEdit]);

  const handleSubmit = async () => {
    setSaving(true);
    setFieldErrors({});
    try {
      if (isEdit && listingId) {
        await marketplaceApi.updateListing(listingId, form);
        navigate(`/kisan/listing/${listingId}`);
      } else {
        const created = await marketplaceApi.createListing(form);
        navigate(`/kisan/listing/${created.id}`);
      }
    } catch (err) {
      const parsed = parseApiError(err);
      setError(parsed.message);
      if (parsed.fieldErrors) setFieldErrors(parsed.fieldErrors);
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <PageSkeleton />;
  if (error && !form.title) return <ErrorState message={error} />;

  return (
    <div className="space-y-6 animate-fade-in max-w-2xl mx-auto">
      <button
        onClick={() => navigate(isEdit ? `/kisan/listing/${listingId}` : '/kisan')}
        className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> {t('common.back')}
      </button>

      <div>
        <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-teal-500 to-cyan-600 flex items-center justify-center">
            <Store className="h-5 w-5 text-white" />
          </div>
          {isEdit ? t('marketplace.editListing') : t('marketplace.createListing')}
        </h1>
      </div>

      <Card padding="md">
        <div className="space-y-4">
          <div>
            <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.title')} *</label>
            <input value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              maxLength={150} placeholder={t('marketplace.listingForm.titlePlaceholder')}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500" />
            {fieldErrors.Title && <p className="text-xs text-red-500 mt-1">{fieldErrors.Title[0]}</p>}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.category')} *</label>
              <input value={form.category} onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))}
                maxLength={50} placeholder={t('marketplace.listingForm.categoryPlaceholder')}
                className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500" />
            </div>
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.listingType')} *</label>
              <select value={form.listingType} onChange={(e) => setForm((f) => ({ ...f, listingType: e.target.value }))}
                className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500">
                <option value="Sale">{t('marketplace.sale')}</option>
                <option value="Rent">{t('marketplace.rent')}</option>
              </select>
            </div>
          </div>

          <div>
            <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.description')} *</label>
            <textarea value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              maxLength={2000} rows={4} placeholder={t('marketplace.listingForm.descriptionPlaceholder')}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none" />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.price')} *</label>
              <input type="number" value={form.price || ''} onChange={(e) => setForm((f) => ({ ...f, price: Number(e.target.value) }))}
                min={0} className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500" />
            </div>
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.priceUnit')} *</label>
              <select value={form.priceUnit} onChange={(e) => setForm((f) => ({ ...f, priceUnit: e.target.value }))}
                className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500">
                <option value="Total">{t('marketplace.priceUnit.total')}</option>
                <option value="Day">{t('marketplace.priceUnit.day')}</option>
                <option value="Hour">{t('marketplace.priceUnit.hour')}</option>
                <option value="Week">{t('marketplace.priceUnit.week')}</option>
                <option value="Month">{t('marketplace.priceUnit.month')}</option>
              </select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.location')} *</label>
              <input value={form.location} onChange={(e) => setForm((f) => ({ ...f, location: e.target.value }))}
                maxLength={200} placeholder={t('marketplace.listingForm.locationPlaceholder')}
                className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500" />
            </div>
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.contactNumber')} *</label>
              <input value={form.contactNumber} onChange={(e) => setForm((f) => ({ ...f, contactNumber: e.target.value }))}
                maxLength={30} placeholder={t('marketplace.listingForm.contactPlaceholder')}
                className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500" />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.condition')} *</label>
              <select value={form.condition} onChange={(e) => setForm((f) => ({ ...f, condition: e.target.value }))}
                className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500">
                <option value="New">{t('marketplace.new')}</option>
                <option value="Used">{t('marketplace.used')}</option>
              </select>
            </div>
            <div>
              <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.availability')} *</label>
              <input value={form.availability} onChange={(e) => setForm((f) => ({ ...f, availability: e.target.value }))}
                maxLength={100} placeholder={t('marketplace.listingForm.availabilityPlaceholder')}
                className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500" />
            </div>
          </div>

          <div>
            <label className="text-xs font-medium text-gray-700 mb-1 block">{t('marketplace.listingForm.imageUrl')}</label>
            <input type="url" value={form.imageUrl || ''} onChange={(e) => setForm((f) => ({ ...f, imageUrl: e.target.value || null }))}
              placeholder="https://..."
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500" />
          </div>

          {error && <p className="text-sm text-red-600">{error}</p>}

          <div className="flex justify-end gap-3 pt-4 border-t border-gray-100">
            <Button variant="secondary" onClick={() => navigate(isEdit ? `/kisan/listing/${listingId}` : '/kisan')}>
              {t('common.cancel')}
            </Button>
            <Button variant="primary" loading={saving} onClick={handleSubmit}>
              {isEdit ? t('marketplace.listingForm.update') : t('marketplace.listingForm.submit')}
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
