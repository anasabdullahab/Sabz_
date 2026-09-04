import { apiClient } from './client';
import type {
  MonitoringCheckDto,
  MonitoringCompletionResponseDto,
  MonitoringGenerationResultDto,
  CompleteMonitoringCheckRequestDto,
  SkipMonitoringCheckRequestDto,
} from '@/types';

export const monitoringApi = {
  getChecksForCrop(cropId: string) {
    return apiClient.get<MonitoringCheckDto[]>(`/api/crops/${cropId}/monitoring`).then((r) => r.data);
  },

  generateChecks(cropId: string) {
    return apiClient.post<MonitoringGenerationResultDto>(`/api/crops/${cropId}/monitoring/generate`).then((r) => r.data);
  },

  getDue() {
    return apiClient.get<MonitoringCheckDto[]>('/api/monitoring/due').then((r) => r.data);
  },

  getUpcoming() {
    return apiClient.get<MonitoringCheckDto[]>('/api/monitoring/upcoming').then((r) => r.data);
  },

  complete(checkId: string, data: CompleteMonitoringCheckRequestDto) {
    return apiClient
      .post<MonitoringCompletionResponseDto>(`/api/monitoring/${checkId}/complete`, data)
      .then((r) => r.data);
  },

  skip(checkId: string, notes?: string | null) {
    const body: SkipMonitoringCheckRequestDto = { notes: notes ?? null };
    return apiClient.post<MonitoringCheckDto>(`/api/monitoring/${checkId}/skip`, body).then((r) => r.data);
  },
};
