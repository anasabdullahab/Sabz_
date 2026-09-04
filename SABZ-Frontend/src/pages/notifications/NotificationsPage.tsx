import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { notificationApi } from '@/api/notificationApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { ErrorState } from '@/components/ui/EmptyState';
import { formatDateTime, cn } from '@/lib/utils';
import { t } from '@/lib/i18n';
import {
  Bell, BellOff, CheckCheck, MailOpen, Clock,
  CalendarCheck, Info,
} from 'lucide-react';
import type { NotificationDto } from '@/types';

function categoryIcon(category: string) {
  switch (category) {
    case 'MonitoringPlan':
    case 'MonitoringDue':
    case 'MonitoringUpcoming':
    case 'MonitoringCompleted':
    case 'MonitoringSkipped':
      return CalendarCheck;
    case 'System':
      return Info;
    default:
      return Bell;
  }
}

function categoryBadge(category: string): 'danger' | 'warning' | 'info' | 'success' | 'neutral' | 'primary' {
  switch (category) {
    case 'MonitoringPlan': return 'primary';
    case 'MonitoringDue': return 'danger';
    case 'MonitoringUpcoming': return 'info';
    case 'MonitoringCompleted': return 'success';
    case 'MonitoringSkipped': return 'warning';
    default: return 'neutral';
  }
}

export function NotificationsPage() {
  const navigate = useNavigate();
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'unread'>('all');
  const [markingAll, setMarkingAll] = useState(false);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await notificationApi.getAll(100);
      setNotifications(data);
    } catch (err) {
      setError(parseApiError(err).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleMarkRead = async (id: string) => {
    try {
      await notificationApi.markRead(id);
      setNotifications((prev) =>
        prev.map((n) => (n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n)),
      );
    } catch { /* silent */ }
  };

  const handleMarkAllRead = async () => {
    setMarkingAll(true);
    try {
      await notificationApi.markAllRead();
      setNotifications((prev) =>
        prev.map((n) => ({ ...n, isRead: true, readAt: new Date().toISOString() })),
      );
    } catch { /* silent */ } finally {
      setMarkingAll(false);
    }
  };

  const handleNotificationClick = (n: NotificationDto) => {
    if (!n.isRead) handleMarkRead(n.id);

    // Navigate based on reference type
    if (n.referenceType === 'CropMonitoringCheck' && n.referenceId) {
      // We don't have farmId in the notification, but referenceId is the checkId
      // Navigate to monitoring page
      navigate('/monitoring');
    }
  };

  if (loading) return <PageSkeleton />;
  if (error) return <ErrorState message={error} onRetry={load} />;

  const filtered = filter === 'unread' ? notifications.filter((n) => !n.isRead) : notifications;
  const unreadCount = notifications.filter((n) => !n.isRead).length;

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl lg:text-3xl font-bold text-gray-900 flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-violet-500 to-indigo-500 flex items-center justify-center">
              <Bell className="h-5 w-5 text-white" />
            </div>
            {t('notifications.title')}
          </h1>
          <p className="text-gray-500 mt-1 ml-[52px]">
            {unreadCount > 0 ? `${unreadCount} unread` : t('notifications.empty')}
          </p>
        </div>
        {unreadCount > 0 && (
          <Button variant="outline" size="sm" loading={markingAll} onClick={handleMarkAllRead}>
            <CheckCheck className="h-4 w-4" /> {t('notifications.markAllRead')}
          </Button>
        )}
      </div>

      {/* Filter tabs */}
      <div className="flex gap-1 p-1 bg-gray-100 rounded-xl w-fit">
        <button
          onClick={() => setFilter('all')}
          className={cn(
            'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
            filter === 'all' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700',
          )}
        >
          {t('notifications.all')} ({notifications.length})
        </button>
        <button
          onClick={() => setFilter('unread')}
          className={cn(
            'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
            filter === 'unread' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700',
          )}
        >
          {t('notifications.unread')} ({unreadCount})
        </button>
      </div>

      {/* Notification list */}
      {filtered.length === 0 ? (
        <EmptyState
          icon={<BellOff className="h-16 w-16" />}
          title={filter === 'unread' ? t('notifications.emptyUnread') : t('notifications.empty')}
        />
      ) : (
        <div className="space-y-2">
          {filtered.map((n) => {
            const Icon = categoryIcon(n.category);
            return (
              <Card
                key={n.id}
                padding="sm"
                hover
                onClick={() => handleNotificationClick(n)}
                className={cn(
                  'transition-all',
                  !n.isRead && 'border-l-4 border-l-primary-500 bg-primary-50/30',
                )}
              >
                <div className="flex items-start gap-3">
                  <div className={cn(
                    'h-9 w-9 rounded-lg flex items-center justify-center shrink-0',
                    n.isRead ? 'bg-gray-100' : 'bg-primary-100',
                  )}>
                    <Icon className={cn('h-4 w-4', n.isRead ? 'text-gray-500' : 'text-primary-600')} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-0.5">
                      <h3 className={cn('text-sm truncate', n.isRead ? 'font-medium text-gray-700' : 'font-semibold text-gray-900')}>
                        {n.title}
                      </h3>
                      {!n.isRead && (
                        <span className="h-2 w-2 rounded-full bg-primary-500 shrink-0" />
                      )}
                    </div>
                    <p className="text-xs text-gray-600 line-clamp-2">{n.message}</p>
                    <div className="flex items-center gap-2 mt-1.5">
                      <Badge variant={categoryBadge(n.category)} size="sm">
                        {n.category.replace(/([A-Z])/g, ' $1').trim()}
                      </Badge>
                      <span className="text-[10px] text-gray-400 flex items-center gap-1">
                        <Clock className="h-3 w-3" />
                        {formatDateTime(n.createdAt)}
                      </span>
                      {n.isRead && n.readAt && (
                        <span className="text-[10px] text-gray-400">
                          · Read {formatDateTime(n.readAt)}
                        </span>
                      )}
                    </div>
                  </div>
                  {!n.isRead && (
                    <button
                      onClick={(e) => { e.stopPropagation(); handleMarkRead(n.id); }}
                      className="p-1.5 rounded-lg text-gray-400 hover:text-primary-600 hover:bg-primary-50 transition-colors shrink-0"
                      title={t('notifications.markRead')}
                    >
                      <MailOpen className="h-4 w-4" />
                    </button>
                  )}
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
