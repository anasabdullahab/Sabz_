import { apiClient } from './client';
import type { AgronomistResponseDto, VoiceAgronomistResponseDto } from '@/types';

export const agronomistApi = {
  chat(farmId: string, message: string) {
    return apiClient
      .post<AgronomistResponseDto>(`/api/farms/${farmId}/agronomist/chat`, { message })
      .then((r) => r.data);
  },

  voice(farmId: string, audioFile: File) {
    const form = new FormData();
    form.append('audio', audioFile);
    return apiClient
      .post<VoiceAgronomistResponseDto>(`/api/farms/${farmId}/agronomist/voice`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
};
