import { apiClient } from './client';
import type { WeatherResponseDto, WeatherPreviewDto, WeatherAlertsResponseDto, ReverseGeocodeDto } from '@/types';

export const weatherApi = {
  getCurrent(farmId: string, latitude?: number, longitude?: number) {
    const params = new URLSearchParams();
    if (latitude != null) params.set('latitude', latitude.toString());
    if (longitude != null) params.set('longitude', longitude.toString());
    const qs = params.toString();
    return apiClient
      .get<WeatherResponseDto>(`/api/farms/${farmId}/weather/current${qs ? `?${qs}` : ''}`)
      .then((r) => r.data);
  },

  getForecast(farmId: string, latitude?: number, longitude?: number) {
    const params = new URLSearchParams();
    if (latitude != null) params.set('latitude', latitude.toString());
    if (longitude != null) params.set('longitude', longitude.toString());
    const qs = params.toString();
    return apiClient
      .get<WeatherResponseDto>(`/api/farms/${farmId}/weather/forecast${qs ? `?${qs}` : ''}`)
      .then((r) => r.data);
  },

  reverseGeocode(farmId: string, latitude: number, longitude: number) {
    return apiClient
      .get<ReverseGeocodeDto>(`/api/farms/${farmId}/weather/reverse-geocode?latitude=${latitude}&longitude=${longitude}`)
      .then((r) => r.data);
  },

  /** Tehsil-based preview (no farm required) — dashboard onboarding weather card. */
  getPreview(tehsilId: number) {
    return apiClient
      .get<WeatherPreviewDto>(`/api/weather/preview?tehsilId=${tehsilId}`)
      .then((r) => r.data);
  },

  /** Smart rule-based farm weather action alerts (rain/fungal/wind/frost/heat). */
  getAlerts(farmId: string) {
    return apiClient
      .get<WeatherAlertsResponseDto>(`/api/farms/${farmId}/weather/alerts`)
      .then((r) => r.data);
  },
};
