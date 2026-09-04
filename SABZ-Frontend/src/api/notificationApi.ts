import { apiClient } from './client';
import type { NotificationDto, UnreadCountResponseDto, MarkAllReadResponseDto } from '@/types';

export const notificationApi = {
  getAll(take?: number) {
    const params = take ? `?take=${take}` : '';
    return apiClient.get<NotificationDto[]>(`/api/notifications${params}`).then((r) => r.data);
  },

  getUnread() {
    return apiClient.get<NotificationDto[]>('/api/notifications/unread').then((r) => r.data);
  },

  getUnreadCount() {
    return apiClient.get<UnreadCountResponseDto>('/api/notifications/unread-count').then((r) => r.data);
  },

  markRead(notificationId: string) {
    return apiClient.patch<NotificationDto>(`/api/notifications/${notificationId}/read`).then((r) => r.data);
  },

  markAllRead() {
    return apiClient.patch<MarkAllReadResponseDto>('/api/notifications/read-all').then((r) => r.data);
  },
};
