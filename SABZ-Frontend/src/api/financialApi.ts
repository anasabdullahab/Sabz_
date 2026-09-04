import { apiClient } from './client';
import type {
  FinancialTransactionResponseDto,
  FinancialSummaryResponseDto,
  CreateFinancialTransactionDto,
  UpdateFinancialTransactionDto,
} from '@/types';

export const financialApi = {
  create(farmId: string, data: CreateFinancialTransactionDto) {
    return apiClient
      .post<FinancialTransactionResponseDto>(`/api/farms/${farmId}/transactions`, data)
      .then((r) => r.data);
  },

  getByFarm(
    farmId: string,
    params?: { type?: string; category?: string; cropId?: string; fromDate?: string; toDate?: string; take?: number },
  ) {
    const q = new URLSearchParams();
    if (params?.type) q.set('type', params.type);
    if (params?.category) q.set('category', params.category);
    if (params?.cropId) q.set('cropId', params.cropId);
    if (params?.fromDate) q.set('fromDate', params.fromDate);
    if (params?.toDate) q.set('toDate', params.toDate);
    if (params?.take) q.set('take', String(params.take));
    const qs = q.toString();
    return apiClient
      .get<FinancialTransactionResponseDto[]>(`/api/farms/${farmId}/transactions${qs ? `?${qs}` : ''}`)
      .then((r) => r.data);
  },

  getById(id: string) {
    return apiClient.get<FinancialTransactionResponseDto>(`/api/transactions/${id}`).then((r) => r.data);
  },

  update(id: string, data: UpdateFinancialTransactionDto) {
    return apiClient
      .put<FinancialTransactionResponseDto>(`/api/transactions/${id}`, data)
      .then((r) => r.data);
  },

  delete(id: string) {
    return apiClient.delete(`/api/transactions/${id}`);
  },

  getSummary(farmId: string, params?: { cropId?: string; fromDate?: string; toDate?: string }) {
    const q = new URLSearchParams();
    if (params?.cropId) q.set('cropId', params.cropId);
    if (params?.fromDate) q.set('fromDate', params.fromDate);
    if (params?.toDate) q.set('toDate', params.toDate);
    const qs = q.toString();
    return apiClient
      .get<FinancialSummaryResponseDto>(`/api/farms/${farmId}/financial-summary${qs ? `?${qs}` : ''}`)
      .then((r) => r.data);
  },
};
