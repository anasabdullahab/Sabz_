import { apiClient } from './client';
import type { AuthResponse, LoginRequest, RegisterRequest, UserResponse } from '@/types';

export const authApi = {
  login(data: LoginRequest) {
    return apiClient.post<AuthResponse>('/api/auth/login', data).then((r) => r.data);
  },

  register(data: RegisterRequest) {
    return apiClient.post<AuthResponse>('/api/auth/register', data).then((r) => r.data);
  },

  getMe() {
    return apiClient.get<UserResponse>('/api/auth/me').then((r) => r.data);
  },
};
