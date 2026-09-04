import { apiClient } from './client';
import type { DiseaseDetectionResponseDto } from '@/types';

export const diseaseApi = {
  detect(farmId: string, image: File, cropId?: string | null, notes?: string | null) {
    const formData = new FormData();
    formData.append('image', image);
    if (cropId) formData.append('cropId', cropId);
    if (notes) formData.append('notes', notes);

    return apiClient
      .post<DiseaseDetectionResponseDto>(`/api/farms/${farmId}/disease-detection`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 120_000,
      })
      .then((r) => r.data);
  },
};
