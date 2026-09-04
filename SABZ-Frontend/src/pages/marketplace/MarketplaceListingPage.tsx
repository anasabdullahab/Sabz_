import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { marketplaceApi } from '@/api/marketplaceApi';
import { inboxApi } from '@/api/inboxApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Modal } from '@/components/ui/Modal';
import { ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { formatDate, cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  ArrowLeft, MapPin, Tag, Package, Edit, Trash2, MessageSquare, Phone, Clock,
} from 'lucide-react';
import type { MarketplaceListingResponseDto } from '@/types';

export function MarketplaceListingPage() {
  const { listingId } = useParams<{ listingId: string }>();
  const navigate = useNavigate();
  const [listing, setListing] = useState<MarketplaceListingResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [contactOpen, setContactOpen] = useState(false);
  const [contactMsg, setContactMsg] = useState('');
  const [contacting, setContacting] = useState(false);

  useEffect(() => {
    if (!listingId) return;
    marketplaceApi.getListing(listingId)
      .then(setListing)
      .catch((err) => setError(parseApiError(err).message))
      .finally(() => setLoading(false));
  }, [listingId]);

  const handleDelete = async () => {
    if (!listingId) return;
    setDeleting(true);
    try {
      await marketplaceApi.deleteListing(listingId);
      navigate('/kisan');
    } catch (err) {
      setError(parseApiError(err).message);
      setDeleteOpen(false);
    } finally {
      setDeleting(false);
    }
  };

  const handleContact = async () => {
    if (!listingId || !contactMsg.trim()) return;
    setContacting(true);
    try {
      const conv = await inboxApi.contactSeller(listingId, contactMsg.trim());
      navigate(`/kisan/conversation/${conv.conversationId}`);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setContacting(false);
    }
  };

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} />;
  if (!listing) return null;

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <button
          onClick={() => navigate('/kisan')}
          className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" /> {t('common.back')}
        </button>
        <div className="flex gap-2">
          {listing.isOwnedByCurrentUser ? (
            <>
              <Button variant="secondary" size="sm" onClick={() => navigate(`/kisan/listing/${listing.id}/edit`)}>
                <Edit className="h-4 w-4" /> {t('common.edit')}
              </Button>
              <Button variant="danger" size="sm" onClick={() => setDeleteOpen(true)}>
                <Trash2 className="h-4 w-4" /> {t('common.delete')}
              </Button>
            </>
          ) : (
            <Button variant="primary" size="sm" onClick={() => setContactOpen(true)}>
              <MessageSquare className="h-4 w-4" /> {t('marketplace.messageSeller')}
            </Button>
          )}
        </div>
      </div>

      {/* Image */}
      {listing.imageUrl ? (
        <img
          src={listing.imageUrl}
          alt={listing.title}
          className="w-full max-h-80 object-cover rounded-2xl border border-gray-100"
          onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
        />
      ) : (
        <div className="w-full h-48 bg-gray-100 rounded-2xl flex items-center justify-center">
          <Package className="h-16 w-16 text-gray-300" />
        </div>
      )}

      {/* Details */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-4">
          <Card padding="md">
            <div className="flex items-start justify-between mb-3">
              <h1 className="text-xl font-bold text-gray-900">{listing.title}</h1>
              <Badge variant={listing.listingType === 'Sale' ? 'success' : 'info'}>{listing.listingType}</Badge>
            </div>
            <p className="text-2xl font-bold text-primary-700 mb-4">
              PKR {listing.price.toLocaleString('en-PK')}
              <span className="text-sm text-gray-400 font-normal ml-1">/{listing.priceUnit}</span>
            </p>
            <p className="text-sm text-gray-700 whitespace-pre-wrap">{listing.description}</p>
          </Card>
        </div>

        <div className="space-y-4">
          <Card padding="sm">
            <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Details</h3>
            <div className="space-y-3">
              <DetailRow icon={Tag} label={t('marketplace.listingForm.category')} value={listing.category} />
              <DetailRow icon={Package} label={t('marketplace.listingForm.condition')} value={listing.condition} />
              <DetailRow icon={MapPin} label={t('marketplace.listingForm.location')} value={listing.location} />
              <DetailRow icon={Clock} label="Availability" value={listing.availability} />
            </div>
          </Card>

          <Card padding="sm">
            <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Seller</h3>
            <div className="flex items-center gap-2 mb-2">
              <div className="h-8 w-8 rounded-full bg-primary-100 flex items-center justify-center">
                <span className="text-xs font-bold text-primary-700">{listing.sellerName.charAt(0).toUpperCase()}</span>
              </div>
              <p className="text-sm font-medium text-gray-900">{listing.sellerName}</p>
            </div>
            {listing.contactNumber && listing.isOwnedByCurrentUser && (
              <p className="flex items-center gap-2 text-sm text-gray-600 mt-2">
                <Phone className="h-3.5 w-3.5" /> {listing.contactNumber}
              </p>
            )}
            <p className="text-[10px] text-gray-400 mt-2">Listed {formatDate(listing.createdAt)}</p>
          </Card>
        </div>
      </div>

      {/* Delete modal */}
      <Modal open={deleteOpen} onClose={() => setDeleteOpen(false)} title={t('marketplace.deleteListing')}>
        <p className="text-sm text-gray-600 mb-6">{t('marketplace.deleteConfirm')}</p>
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={() => setDeleteOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="danger" loading={deleting} onClick={handleDelete}>{t('common.delete')}</Button>
        </div>
      </Modal>

      {/* Contact seller modal */}
      <Modal open={contactOpen} onClose={() => setContactOpen(false)} title={t('marketplace.messageSeller')}>
        <textarea
          value={contactMsg}
          onChange={(e) => setContactMsg(e.target.value)}
          placeholder={t('inbox.messagePlaceholder')}
          maxLength={2000}
          rows={3}
          className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none mb-4"
        />
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={() => setContactOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={contacting} disabled={!contactMsg.trim()} onClick={handleContact}>
            {t('inbox.send')}
          </Button>
        </div>
      </Modal>
    </div>
  );
}

function DetailRow({ icon: Icon, label, value }: { icon: React.ElementType; label: string; value: string }) {
  return (
    <div className="flex items-center gap-2">
      <Icon className="h-3.5 w-3.5 text-gray-400 shrink-0" />
      <span className="text-xs text-gray-500">{label}:</span>
      <span className="text-xs font-medium text-gray-900">{value}</span>
    </div>
  );
}
