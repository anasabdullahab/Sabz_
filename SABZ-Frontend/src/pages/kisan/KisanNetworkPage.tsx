import { useEffect, useState, useCallback } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { communityApi } from '@/api/communityApi';
import { marketplaceApi } from '@/api/marketplaceApi';
import { inboxApi } from '@/api/inboxApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { EmptyState, ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { formatDate, cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  Users, MessageSquare, Plus, Send, Image, Trash2, Store, Tag,
  MapPin, Package, Clock, Search,
} from 'lucide-react';
import type {
  CommunityPostResponseDto,
  MarketplaceListingSummaryDto,
  MarketplaceConversationSummaryDto,
  PagedResultDto,
  MarketplacePagedResultDto,
  MarketplaceInboxPagedResultDto,
} from '@/types';

type Tab = 'feed' | 'messages';
type FeedFilter = 'all' | 'posts' | 'listings';

/**
 * Kisan Network (overhauled): one unified view merging the farmer community
 * forum, the marketplace and the private inbox.
 *   - Feed tab: combined social posts + buy/sell/rent listings
 *   - Messages tab: direct conversations with buyers and sellers
 */
export function KisanNetworkPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  // Tab state lives in the URL (?tab=messages) so deep links like the
  // conversation back-button land directly on the Messages tab.
  const tab: Tab = searchParams.get('tab') === 'messages' ? 'messages' : 'feed';
  const setTab = (next: Tab) => {
    if (next === tab) return;
    setSearchParams(next === 'feed' ? {} : { tab: next });
  };

  return (
    <div className="space-y-6 animate-fade-in">
      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-orange-500 to-amber-600 flex items-center justify-center">
            <Users className="h-5 w-5 text-white" />
          </div>
          {t('kisan.title')}
        </h1>
        <p className="text-gray-500 mt-1 ml-[52px]">{t('kisan.description')}</p>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 bg-gray-100 rounded-xl p-1 w-fit">
        <TabButton active={tab === 'feed'} onClick={() => setTab('feed')} icon={MessageSquare}>
          {t('kisan.feed')}
        </TabButton>
        <TabButton active={tab === 'messages'} onClick={() => setTab('messages')} icon={Store}>
          {t('kisan.messages')}
        </TabButton>
      </div>

      {tab === 'feed' ? <FeedTab navigate={navigate} /> : <MessagesTab navigate={navigate} />}
    </div>
  );
}

function TabButton({
  active, onClick, icon: Icon, children,
}: { active: boolean; onClick: () => void; icon: React.ElementType; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors',
        active ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700',
      )}
    >
      <Icon className="h-4 w-4" />
      {children}
    </button>
  );
}

// ---------------------------------------------------------------------
//  Feed tab
// ---------------------------------------------------------------------

function FeedTab({ navigate }: { navigate: (to: string) => void }) {
  const [filter, setFilter] = useState<FeedFilter>('all');
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="space-y-4">
      {/* Filter chips + actions */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex gap-1.5">
          {(['all', 'posts', 'listings'] as FeedFilter[]).map((f) => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={cn(
                'px-3 py-1.5 rounded-full text-xs font-medium transition-colors',
                filter === f
                  ? 'bg-primary-700 text-white'
                  : 'bg-gray-100 text-gray-600 hover:bg-gray-200',
              )}
            >
              {t(`kisan.${f}`)}
            </button>
          ))}
        </div>
        <Button variant="primary" size="sm" onClick={() => navigate('/kisan/listing/new')}>
          <Tag className="h-4 w-4" /> {t('kisan.sellItem')}
        </Button>
      </div>

      {error && <ErrorState message={error} onRetry={() => setError(null)} />}

      {filter === 'listings'
        ? <ListingsView onOpen={(id) => navigate(`/kisan/listing/${id}`)} />
        : <PostsView
            filter={filter}
            onOpenPost={(id) => navigate(`/kisan/post/${id}`)}
            onOpenListing={(id) => navigate(`/kisan/listing/${id}`)}
          />}
    </div>
  );
}

/** Combined "All" feed (posts + listings interleaved newest-first) or posts-only. */
function PostsView({
  filter,
  onOpenPost,
  onOpenListing,
}: {
  filter: FeedFilter;
  onOpenPost: (id: string) => void;
  onOpenListing: (id: string) => void;
}) {
  const [posts, setPosts] = useState<CommunityPostResponseDto[]>([]);
  const [listings, setListings] = useState<MarketplaceListingSummaryDto[]>([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [content, setContent] = useState('');
  const [imageUrl, setImageUrl] = useState('');
  const [publishing, setPublishing] = useState(false);

  const load = useCallback(async (nextPage: number, append: boolean) => {
    setLoading(true);
    setError(null);
    try {
      const postsPromise = communityApi.getPosts(nextPage, filter === 'all' ? 10 : 20);
      const listingsPromise = filter === 'all'
        ? marketplaceApi.getListings({ page: nextPage, pageSize: 10 })
        : Promise.resolve(null);

      const [postsRes, listingsRes] = await Promise.all([postsPromise, listingsPromise]);
      setPosts((prev) => append ? [...prev, ...postsRes.items] : postsRes.items);
      setListings((prev) =>
        append ? [...prev, ...(listingsRes?.items ?? [])] : (listingsRes?.items ?? []));

      const morePosts = postsRes.page < postsRes.totalPages;
      const moreListings = listingsRes ? listingsRes.page < listingsRes.totalPages : false;
      setHasMore(morePosts || moreListings);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => {
    setPage(1);
    setPosts([]);
    setListings([]);
    setHasMore(true);
    load(1, false);
  }, [load]);

  const handlePublish = async () => {
    if (!content.trim() || publishing) return;
    setPublishing(true);
    try {
      await communityApi.createPost(content.trim(), imageUrl.trim() || undefined);
      setContent('');
      setImageUrl('');
      setShowForm(false);
      setPage(1);
      load(1, false);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setPublishing(false);
    }
  };

  const handleDeletePost = async (postId: string) => {
    if (!window.confirm(t('community.deleteConfirm'))) return;
    try {
      await communityApi.deletePost(postId);
      setPosts((prev) => prev.filter((p) => p.id !== postId));
    } catch (err) {
      setError(parseApiError(err).message);
    }
  };

  // Interleave by createdAt (newest first) in the "all" view.
  const items: Array<{ kind: 'post'; data: CommunityPostResponseDto } | { kind: 'listing'; data: MarketplaceListingSummaryDto }> = [
    ...posts.map((p) => ({ kind: 'post' as const, data: p })),
    ...(filter === 'all' ? listings.map((l) => ({ kind: 'listing' as const, data: l })) : []),
  ].sort((a, b) => new Date(b.data.createdAt).getTime() - new Date(a.data.createdAt).getTime());

  if (loading && items.length === 0) return <PageSkeleton />;

  return (
    <div className="space-y-4">
      {/* Create post */}
      <Card padding="sm" className="bg-white">
        {showForm ? (
          <div>
            <textarea
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder={t('community.postPlaceholder')}
              maxLength={2000}
              rows={3}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none"
            />
            <div className="flex items-center justify-between mt-2">
              <div className="flex items-center gap-2">
                <Image className="h-4 w-4 text-gray-400" />
                <input
                  type="url"
                  value={imageUrl}
                  onChange={(e) => setImageUrl(e.target.value)}
                  placeholder={t('community.imageUrlPlaceholder')}
                  maxLength={2048}
                  className="px-3 py-1.5 rounded-lg border border-gray-200 text-xs w-48 focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
              <div className="flex items-center gap-2">
                <Button variant="secondary" size="sm" onClick={() => setShowForm(false)}>
                  {t('common.cancel')}
                </Button>
                <Button variant="primary" size="sm" onClick={handlePublish} loading={publishing} disabled={!content.trim()}>
                  <Send className="h-3.5 w-3.5" /> {t('community.publish')}
                </Button>
              </div>
            </div>
          </div>
        ) : (
          <button
            onClick={() => setShowForm(true)}
            className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl border border-dashed border-gray-300 text-sm text-gray-500 hover:border-primary-400 hover:text-primary-700 hover:bg-primary-50/50 transition-colors"
          >
            <Plus className="h-4 w-4" />
            {t('community.createPost')}
          </button>
        )}
      </Card>

      {items.length === 0 && !loading && (
        <EmptyState icon={<MessageSquare className="h-16 w-16" />} title={t('community.noPosts')} />
      )}

      {/* Combined feed */}
      <div className="space-y-3">
        {items.map((item) => item.kind === 'post'
          ? <PostCard key={`p-${item.data.id}`} post={item.data} onClick={() => onOpenPost(item.data.id)} onDelete={handleDeletePost} />
          : <ListingCard key={`l-${item.data.id}`} listing={item.data} onClick={() => onOpenListing(item.data.id)} />)}
      </div>

      {hasMore && items.length > 0 && (
        <div className="flex justify-center pt-2">
          <Button
            variant="secondary"
            size="sm"
            loading={loading}
            onClick={() => { const next = page + 1; setPage(next); load(next, true); }}
          >
            {t('kisan.loadMore')}
          </Button>
        </div>
      )}
    </div>
  );
}

function PostCard({
  post, onClick, onDelete,
}: {
  post: CommunityPostResponseDto;
  onClick: () => void;
  onDelete: (id: string) => void;
}) {
  return (
    <Card padding="sm" hover onClick={onClick}>
      <div className="flex items-start justify-between mb-2">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-full bg-primary-100 flex items-center justify-center">
            <span className="text-xs font-bold text-primary-700">{post.authorName.charAt(0).toUpperCase()}</span>
          </div>
          <div>
            <p className="text-sm font-medium text-gray-900">{post.authorName}</p>
            <p className="text-[10px] text-gray-400">{formatDate(post.createdAt)}</p>
          </div>
        </div>
        <div className="flex items-center gap-1">
          <Badge variant="neutral" size="sm">{t('kisan.post')}</Badge>
          {post.isOwnedByCurrentUser && (
            <button
              onClick={(e) => { e.stopPropagation(); onDelete(post.id); }}
              className="p-1.5 rounded-lg text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      </div>

      <p className="text-sm text-gray-700 whitespace-pre-wrap mb-3">{post.content}</p>

      {post.imageUrl && (
        <img
          src={post.imageUrl}
          alt="Post attachment"
          className="w-full max-h-64 object-cover rounded-lg mb-3 border border-gray-100"
          onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
        />
      )}

      <div className="flex items-center gap-2 text-xs text-gray-400">
        <MessageSquare className="h-3.5 w-3.5" />
        <span>{post.commentCount} {t('community.commentCount')}{post.commentCount !== 1 ? 's' : ''}</span>
      </div>
    </Card>
  );
}

function ListingCard({ listing, onClick }: { listing: MarketplaceListingSummaryDto; onClick: () => void }) {
  return (
    <Card padding="sm" hover onClick={onClick}>
      <div className="flex items-start gap-3">
        {listing.imageUrl ? (
          <img
            src={listing.imageUrl}
            alt={listing.title}
            className="h-16 w-20 rounded-lg object-cover shrink-0 border border-gray-100"
            onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
          />
        ) : (
          <div className="h-16 w-20 rounded-lg bg-gray-100 flex items-center justify-center shrink-0">
            <Package className="h-6 w-6 text-gray-300" />
          </div>
        )}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <h3 className="font-semibold text-sm text-gray-900 truncate">{listing.title}</h3>
            <Badge variant={listing.listingType === 'Sale' ? 'success' : 'info'} size="sm">
              {listing.listingType}
            </Badge>
          </div>
          <p className="text-base font-bold text-primary-700 mt-0.5">
            PKR {listing.price.toLocaleString('en-PK')}
            <span className="text-[10px] text-gray-400 font-normal ml-1">/{listing.priceUnit}</span>
          </p>
          <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[10px] text-gray-500 mt-1">
            <span className="flex items-center gap-1"><MapPin className="h-3 w-3" />{listing.location}</span>
            <span className="flex items-center gap-1"><Tag className="h-3 w-3" />{listing.category}</span>
            <span className="flex items-center gap-1"><Clock className="h-3 w-3" />{formatDate(listing.createdAt)}</span>
          </div>
        </div>
      </div>
    </Card>
  );
}

/** Listings-only view with search + type/condition filters (full marketplace feed). */
function ListingsView({ onOpen }: { onOpen: (id: string) => void }) {
  const [result, setResult] = useState<MarketplacePagedResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [listingType, setListingType] = useState('');
  const [condition, setCondition] = useState('');
  const [page, setPage] = useState(1);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await marketplaceApi.getListings({
        page, pageSize: 20,
        ...(search.trim() ? { search: search.trim() } : {}),
        ...(listingType ? { listingType } : {}),
        ...(condition ? { condition } : {}),
      });
      setResult(data);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  }, [search, listingType, condition, page]);

  useEffect(() => { load(); }, [load]);

  if (loading && !result) return <PageSkeleton />;
  if (error && !result) return <ErrorState message={error} onRetry={load} />;

  return (
    <div className="space-y-4">
      {/* Filters */}
      <Card padding="sm">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex-1 min-w-[180px]">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
              <input
                type="text"
                value={search}
                onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                placeholder={t('marketplace.search')}
                className="w-full pl-9 pr-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
            </div>
          </div>
          <select
            value={listingType}
            onChange={(e) => { setListingType(e.target.value); setPage(1); }}
            className="w-28 px-3 py-2 rounded-lg border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500"
          >
            <option value="">{t('marketplace.allTypes')}</option>
            <option value="Sale">{t('marketplace.sale')}</option>
            <option value="Rent">{t('marketplace.rent')}</option>
          </select>
          <select
            value={condition}
            onChange={(e) => { setCondition(e.target.value); setPage(1); }}
            className="w-28 px-3 py-2 rounded-lg border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500"
          >
            <option value="">{t('marketplace.allConditions')}</option>
            <option value="New">{t('marketplace.new')}</option>
            <option value="Used">{t('marketplace.used')}</option>
          </select>
        </div>
      </Card>

      {result && result.items.length === 0 ? (
        <EmptyState icon={<Store className="h-16 w-16" />} title={t('marketplace.noListings')} />
      ) : result && (
        <div className="space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            {result.items.map((listing) => (
              <MarketplaceCard key={listing.id} listing={listing} onClick={() => onOpen(listing.id)} />
            ))}
          </div>

          {result.totalPages > 1 && (
            <div className="flex justify-center gap-2 pt-4">
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                Previous
              </Button>
              <span className="flex items-center text-xs text-gray-500">{result.page} / {result.totalPages}</span>
              <Button variant="secondary" size="sm" disabled={page >= result.totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function MarketplaceCard({ listing, onClick }: { listing: MarketplaceListingSummaryDto; onClick: () => void }) {
  return (
    <Card padding="none" hover onClick={onClick}>
      {listing.imageUrl ? (
        <img
          src={listing.imageUrl}
          alt={listing.title}
          className="w-full h-36 object-cover rounded-t-2xl"
          onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
        />
      ) : (
        <div className="w-full h-36 bg-gray-100 rounded-t-2xl flex items-center justify-center">
          <Package className="h-10 w-10 text-gray-300" />
        </div>
      )}
      <div className="p-4">
        <div className="flex items-start justify-between mb-2">
          <h3 className="font-semibold text-gray-900 text-sm line-clamp-1">{listing.title}</h3>
          <Badge variant={listing.listingType === 'Sale' ? 'success' : 'info'} size="sm">
            {listing.listingType}
          </Badge>
        </div>
        <p className="text-lg font-bold text-primary-700 mb-2">
          PKR {listing.price.toLocaleString('en-PK')}
          <span className="text-xs text-gray-400 font-normal ml-1">/{listing.priceUnit}</span>
        </p>
        <div className="flex flex-wrap items-center gap-2 text-[10px] text-gray-500">
          <span className="flex items-center gap-1"><Tag className="h-3 w-3" />{listing.category}</span>
          <span className="flex items-center gap-1"><MapPin className="h-3 w-3" />{listing.location}</span>
          <span className="flex items-center gap-1"><Package className="h-3 w-3" />{listing.condition}</span>
        </div>
        <div className="flex items-center justify-between mt-3 pt-2 border-t border-gray-100">
          <span className="text-[10px] text-gray-400">{listing.sellerName}</span>
          <span className="text-[10px] text-gray-400">{formatDate(listing.createdAt)}</span>
        </div>
      </div>
    </Card>
  );
}

// ---------------------------------------------------------------------
//  Messages tab
// ---------------------------------------------------------------------

function MessagesTab({ navigate }: { navigate: (to: string) => void }) {
  const [result, setResult] = useState<MarketplaceInboxPagedResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setResult(await inboxApi.getInbox(page, 20));
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => { load(); }, [load]);

  if (loading && !result) return <PageSkeleton />;
  if (error && !result) return <ErrorState message={error} onRetry={load} />;

  return (
    <div className="space-y-3">
      {result && result.items.length === 0 ? (
        <EmptyState icon={<MessageSquare className="h-16 w-16" />} title={t('inbox.noConversations')} />
      ) : result && (
        <>
          <div className="space-y-2">
            {result.items.map((conv) => (
              <ConversationCard key={conv.conversationId} conv={conv} onClick={() => navigate(`/kisan/conversation/${conv.conversationId}`)} />
            ))}
          </div>
          {result.totalPages > 1 && (
            <div className="flex justify-center gap-2 pt-4">
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                Previous
              </Button>
              <span className="flex items-center text-xs text-gray-500">{result.page} / {result.totalPages}</span>
              <Button variant="secondary" size="sm" disabled={page >= result.totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function ConversationCard({ conv, onClick }: { conv: MarketplaceConversationSummaryDto; onClick: () => void }) {
  return (
    <Card hover onClick={onClick} padding="sm">
      <div className="flex items-start gap-3">
        <div className="h-10 w-10 rounded-full bg-violet-100 flex items-center justify-center shrink-0">
          <Store className="h-5 w-5 text-violet-600" />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <h3 className="font-semibold text-sm text-gray-900 truncate">{conv.listingTitle}</h3>
              <p className="text-xs text-gray-500">{conv.otherParticipantName}</p>
            </div>
            <Badge variant={conv.role === 'Buyer' ? 'info' : 'success'} size="sm">
              {conv.role === 'Buyer' ? t('inbox.buyer') : t('inbox.seller')}
            </Badge>
          </div>
          {conv.latestMessagePreview && (
            <p className="text-xs text-gray-500 mt-1 truncate">{conv.latestMessagePreview}</p>
          )}
          {conv.latestMessageAt && (
            <p className="text-[10px] text-gray-400 mt-1 flex items-center gap-1">
              <Clock className="h-3 w-3" /> {formatDate(conv.latestMessageAt)}
            </p>
          )}
        </div>
      </div>
    </Card>
  );
}
