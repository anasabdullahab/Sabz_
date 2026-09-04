import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { communityApi } from '@/api/communityApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { formatDate } from '@/lib/utils';
import { t } from '@/lib/i18n';
import { ArrowLeft, Send, Trash2, MessageSquare } from 'lucide-react';
import type { CommunityPostDetailDto, CommunityCommentResponseDto } from '@/types';

export function CommunityPostPage() {
  const { postId } = useParams<{ postId: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<CommunityPostDetailDto | null>(null);
  const [comments, setComments] = useState<CommunityCommentResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [commentInput, setCommentInput] = useState('');
  const [sending, setSending] = useState(false);

  const load = async () => {
    if (!postId) return;
    setLoading(true);
    setError(null);
    try {
      const d = await communityApi.getPost(postId);
      setData(d);
      setComments(d.comments);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [postId]);

  const handleComment = async () => {
    if (!commentInput.trim() || !postId || sending) return;
    setSending(true);
    try {
      const newComment = await communityApi.createComment(postId, commentInput.trim());
      setComments((prev) => [...prev, newComment]);
      setCommentInput('');
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setSending(false);
    }
  };

  const handleDeleteComment = async (commentId: string) => {
    if (!window.confirm(t('community.deleteCommentConfirm'))) return;
    try {
      await communityApi.deleteComment(commentId);
      setComments((prev) => prev.filter((c) => c.id !== commentId));
    } catch (err) {
      setError(parseApiError(err).message);
    }
  };

  const handleDeletePost = async () => {
    if (!postId || !window.confirm(t('community.deleteConfirm'))) return;
    try {
      await communityApi.deletePost(postId);
      navigate('/kisan');
    } catch (err) {
      setError(parseApiError(err).message);
    }
  };

  if (loading) return <PageSkeleton />;
  if (error && !data) return <ErrorState message={error} onRetry={load} />;
  if (!data) return null;

  const { post } = data;

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <button
          onClick={() => navigate('/kisan')}
          className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" /> {t('common.back')}
        </button>
        {post.isOwnedByCurrentUser && (
          <Button variant="danger" size="sm" onClick={handleDeletePost}>
            <Trash2 className="h-4 w-4" /> {t('common.delete')}
          </Button>
        )}
      </div>

      {/* Post */}
      <Card padding="md">
        <div className="flex items-center gap-3 mb-4">
          <div className="h-10 w-10 rounded-full bg-primary-100 flex items-center justify-center">
            <span className="text-sm font-bold text-primary-700">
              {post.authorName.charAt(0).toUpperCase()}
            </span>
          </div>
          <div>
            <p className="font-medium text-gray-900">{post.authorName}</p>
            <p className="text-xs text-gray-400">{formatDate(post.createdAt)}</p>
          </div>
        </div>

        <p className="text-gray-700 whitespace-pre-wrap leading-relaxed">{post.content}</p>

        {post.imageUrl && (
          <img
            src={post.imageUrl}
            alt="Post attachment"
            className="w-full max-h-96 object-cover rounded-xl mt-4 border border-gray-100"
            onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
          />
        )}
      </Card>

      {/* Comments */}
      <div>
        <h2 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2">
          <MessageSquare className="h-5 w-5 text-primary-600" />
          {t('community.comments')} ({comments.length})
        </h2>

        {comments.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">{t('community.noComments')}</p>
        ) : (
          <div className="space-y-3">
            {comments.map((comment) => (
              <Card key={comment.id} padding="sm">
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-2 mb-2">
                    <div className="h-7 w-7 rounded-full bg-gray-100 flex items-center justify-center">
                      <span className="text-[10px] font-bold text-gray-600">
                        {comment.authorName.charAt(0).toUpperCase()}
                      </span>
                    </div>
                    <div>
                      <p className="text-xs font-medium text-gray-900">{comment.authorName}</p>
                      <p className="text-[10px] text-gray-400">{formatDate(comment.createdAt)}</p>
                    </div>
                  </div>
                  {comment.isOwnedByCurrentUser && (
                    <button
                      onClick={() => handleDeleteComment(comment.id)}
                      className="p-1 rounded text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors"
                    >
                      <Trash2 className="h-3 w-3" />
                    </button>
                  )}
                </div>
                <p className="text-sm text-gray-700 whitespace-pre-wrap">{comment.content}</p>
              </Card>
            ))}
          </div>
        )}
      </div>

      {/* Comment input */}
      <div className="sticky bottom-0 bg-earth-50 pt-3 pb-2">
        <div className="flex items-center gap-2">
          <input
            type="text"
            value={commentInput}
            onChange={(e) => setCommentInput(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleComment(); } }}
            placeholder={t('community.commentPlaceholder')}
            maxLength={1000}
            disabled={sending}
            className="flex-1 px-4 py-2.5 rounded-xl border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500 disabled:opacity-50"
          />
          <button
            onClick={handleComment}
            disabled={sending || !commentInput.trim()}
            className="h-10 w-10 rounded-xl bg-primary-600 flex items-center justify-center text-white hover:bg-primary-700 transition-colors disabled:opacity-50 shrink-0"
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
