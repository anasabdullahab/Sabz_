import { apiClient } from './client';
import type { FarmDashboardDto } from '@/types';

export const farmDashboardApi = {
  getDashboard(farmId: string) {
    return apiClient
      .get<FarmDashboardDto>(`/api/farms/${farmId}/dashboard`)
      .then((r) => r.data);
  },
};
