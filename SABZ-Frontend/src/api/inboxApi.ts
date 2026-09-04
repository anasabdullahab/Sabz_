import { apiClient } from './client';
import type {
  MarketplaceInboxPagedResultDto,
  MarketplaceConversationDto,
} from '@/types';

export const inboxApi = {
  getInbox(page = 1, pageSize = 20) {
    return apiClient
      .get<MarketplaceInboxPagedResultDto>('/api/marketplace/inbox', { params: { page, pageSize } })
      .then((r) => r.data);
  },

  getConversation(conversationId: string, page = 1, pageSize = 20) {
    return apiClient
      .get<MarketplaceConversationDto>(`/api/marketplace/inbox/${conversationId}`, { params: { page, pageSize } })
      .then((r) => r.data);
  },

  contactSeller(listingId: string, message: string) {
    return apiClient
      .post<MarketplaceConversationDto>(`/api/marketplace/listings/${listingId}/contact`, { message })
      .then((r) => r.data);
  },

  sendMessage(conversationId: string, message: string) {
    return apiClient
      .post(`/api/marketplace/inbox/${conversationId}/messages`, { message })
      .then((r) => r.data);
  },
};
