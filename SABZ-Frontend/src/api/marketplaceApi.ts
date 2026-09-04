import { apiClient } from './client';
import type {
  MarketplaceListingResponseDto,
  MarketplacePagedResultDto,
  CreateMarketplaceListingDto,
  UpdateMarketplaceListingDto,
} from '@/types';

export interface MarketplaceFilters {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: string;
  listingType?: string;
  location?: string;
  condition?: string;
}

export const marketplaceApi = {
  getListings(filters: MarketplaceFilters = {}) {
    const params: Record<string, string | number> = {};
    if (filters.page) params.page = filters.page;
    if (filters.pageSize) params.pageSize = filters.pageSize;
    if (filters.search) params.search = filters.search;
    if (filters.category) params.category = filters.category;
    if (filters.listingType) params.listingType = filters.listingType;
    if (filters.location) params.location = filters.location;
    if (filters.condition) params.condition = filters.condition;
    return apiClient
      .get<MarketplacePagedResultDto>('/api/marketplace/listings', { params })
      .then((r) => r.data);
  },

  createListing(dto: CreateMarketplaceListingDto) {
    return apiClient
      .post<MarketplaceListingResponseDto>('/api/marketplace/listings', dto)
      .then((r) => r.data);
  },

  getListing(listingId: string) {
    return apiClient
      .get<MarketplaceListingResponseDto>(`/api/marketplace/listings/${listingId}`)
      .then((r) => r.data);
  },

  updateListing(listingId: string, dto: UpdateMarketplaceListingDto) {
    return apiClient
      .put<MarketplaceListingResponseDto>(`/api/marketplace/listings/${listingId}`, dto)
      .then((r) => r.data);
  },

  deleteListing(listingId: string) {
    return apiClient.delete(`/api/marketplace/listings/${listingId}`);
  },
};
