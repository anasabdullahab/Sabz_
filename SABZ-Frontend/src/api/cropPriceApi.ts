import { apiClient } from './client';
import type { CropPricePagedResultDto, CropPriceDetailDto } from '@/types';

export interface CropPriceFilters {
  crop?: string;
  province?: string;
  district?: string;
  market?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export const cropPriceApi = {
  getPrices(filters: CropPriceFilters = {}) {
    const params: Record<string, string | number> = {};
    if (filters.crop) params.crop = filters.crop;
    if (filters.province) params.province = filters.province;
    if (filters.district) params.district = filters.district;
    if (filters.market) params.market = filters.market;
    if (filters.fromDate) params.fromDate = filters.fromDate;
    if (filters.toDate) params.toDate = filters.toDate;
    if (filters.page) params.page = filters.page;
    if (filters.pageSize) params.pageSize = filters.pageSize;
    return apiClient
      .get<CropPricePagedResultDto>('/api/crop-prices', { params })
      .then((r) => r.data);
  },

  getPriceByCrop(cropName: string, fromDate?: string, toDate?: string) {
    const params: Record<string, string> = {};
    if (fromDate) params.fromDate = fromDate;
    if (toDate) params.toDate = toDate;
    return apiClient
      .get<CropPriceDetailDto>(`/api/crop-prices/${encodeURIComponent(cropName)}`, { params })
      .then((r) => r.data);
  },
};
