import { apiClient } from './client';
import type { CreateCropDto, CropResponseDto, UpdateCropDto } from '@/types';

export const cropApi = {
  getByFarm(farmId: string) {
    return apiClient.get<CropResponseDto[]>(`/api/farms/${farmId}/crops`).then((r) => r.data);
  },

  getById(id: string) {
    return apiClient.get<CropResponseDto>(`/api/crops/${id}`).then((r) => r.data);
  },

  create(farmId: string, data: CreateCropDto) {
    return apiClient.post<CropResponseDto>(`/api/farms/${farmId}/crops`, data).then((r) => r.data);
  },

  update(id: string, data: UpdateCropDto) {
    return apiClient.put<CropResponseDto>(`/api/crops/${id}`, data).then((r) => r.data);
  },

  delete(id: string) {
    return apiClient.delete(`/api/crops/${id}`);
  },
};
