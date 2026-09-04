import { apiClient } from './client';
import type { FinancialHealthSummaryDto } from '@/types';

export const financialHealthApi = {
  getCropHealth(farmId: string, cropId: string) {
    return apiClient
      .get<FinancialHealthSummaryDto>(`/api/farms/${farmId}/crops/${cropId}/financial-health`)
      .then((r) => r.data);
  },
};
