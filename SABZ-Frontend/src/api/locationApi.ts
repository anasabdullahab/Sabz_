import { apiClient } from './client';
import type { LocationDto } from '@/types';

export const locationApi = {
  getProvinces() {
    return apiClient.get<LocationDto[]>('/api/locations/provinces').then((r) => r.data);
  },

  getDistricts(provinceId: number) {
    return apiClient
      .get<LocationDto[]>(`/api/locations/provinces/${provinceId}/districts`)
      .then((r) => r.data);
  },

  getTehsils(districtId: number) {
    return apiClient
      .get<LocationDto[]>(`/api/locations/districts/${districtId}/tehsils`)
      .then((r) => r.data);
  },
};
