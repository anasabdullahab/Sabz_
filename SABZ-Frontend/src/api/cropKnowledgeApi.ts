import { apiClient } from './client';
import type { CropKnowledgeEntryDto } from '@/types';

/**
 * Local crop knowledge base (embedded JSON in the .NET backend).
 * Anonymous endpoint — powers the searchable crop dropdown, harvest-window
 * estimation and stage progress bars.
 */
export const cropKnowledgeApi = {
  getCrops() {
    return apiClient
      .get<CropKnowledgeEntryDto[]>('/api/crop-knowledge')
      .then((r) => r.data);
  },
};
