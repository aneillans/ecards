import axios from 'axios';
import { getToken, updateToken, login, isLoggedIn } from './auth';

const API_URL = window.ENV?.API_URL || import.meta.env.VITE_API_URL || '/api';

const api = axios.create({ baseURL: API_URL });

api.interceptors.request.use(async (config) => {
  try {
    await updateToken(5);
  } catch (e) {
    console.warn('Token refresh failed:', e);
  }
  const token = getToken();
  if (token) {
    config.headers = config.headers || {};
    config.headers['Authorization'] = `Bearer ${token}`;
  }
  return config;
});

// Global response handler: redirect to login on 401 only if not logged in
api.interceptors.response.use(
  response => response,
  error => {
    const status = error?.response?.status;
    if (status === 401) {
      // Only redirect to login if we don't have a token
      // If we have a token but got 401, it means authorization failed (not authentication)
      if (!isLoggedIn()) {
        console.log('401 and not logged in - redirecting to login');
        try { login(); } catch { /* swallow */ }
      } else {
        console.error('401 error despite being logged in:', error.response?.data);
      }
    }
    return Promise.reject(error);
  }
);

export default api;
