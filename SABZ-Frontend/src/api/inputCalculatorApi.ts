import { apiClient } from './client';
import type { InputCalculatorRequestDto, InputCalculatorResponseDto } from '@/types';

export const inputCalculatorApi = {
  calculate(farmId: string, dto: InputCalculatorRequestDto) {
    return apiClient
      .post<InputCalculatorResponseDto>(`/api/farms/${farmId}/input-calculator`, dto)
      .then((r) => r.data);
  },
};
