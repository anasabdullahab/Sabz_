import axios from 'axios';
import type { ApiErrorResponse } from '@/types';

const TOKEN_KEY = 'sabz_token';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
});

// Attach JWT to every request
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Handle 401 globally
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem(TOKEN_KEY);
      // Only redirect if not already on auth pages
      const path = window.location.pathname;
      if (path !== '/login' && path !== '/register') {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  },
);

/** Extract a user-friendly error message from an API error response */
export function parseApiError(error: unknown): { message: string; fieldErrors?: Record<string, string[]> } {
  if (axios.isAxiosError(error) && error.response) {
    const data = error.response.data as ApiErrorResponse | undefined;
    const status = error.response.status;

    if (data?.message) {
      return {
        message: data.message,
        fieldErrors: data.errors,
      };
    }

    const statusMessages: Record<number, string> = {
      400: 'Please check your input and try again.',
      401: 'Your session has expired. Please log in again.',
      403: "You don't have permission to access this resource.",
      404: "We couldn't find what you're looking for.",
      409: 'A conflict occurred. Please check and try again.',
      502: 'This service is temporarily unavailable. Please try again later.',
      500: 'Something went wrong. Please try again.',
    };

    return { message: statusMessages[status] ?? 'Something went wrong. Please try again.' };
  }

  if (error instanceof Error) {
    return { message: error.message };
  }

  return { message: 'An unexpected error occurred.' };
}

// Token helpers
export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function removeToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

export function isAuthenticated(): boolean {
  return getToken() !== null;
}
