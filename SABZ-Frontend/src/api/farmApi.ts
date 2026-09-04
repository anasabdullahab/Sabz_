import { apiClient } from './client';
import type { CreateFarmDto, FarmResponseDto, UpdateFarmDto } from '@/types';

export const farmApi = {
  getAll() {
    return apiClient.get<FarmResponseDto[]>('/api/farms').then((r) => r.data);
  },

  getById(id: string) {
    return apiClient.get<FarmResponseDto>(`/api/farms/${id}`).then((r) => r.data);
  },

  create(data: CreateFarmDto) {
    return apiClient.post<FarmResponseDto>('/api/farms', data).then((r) => r.data);
  },

  update(id: string, data: UpdateFarmDto) {
    return apiClient.put<FarmResponseDto>(`/api/farms/${id}`, data).then((r) => r.data);
  },

  delete(id: string) {
    return apiClient.delete(`/api/farms/${id}`);
  },
};
