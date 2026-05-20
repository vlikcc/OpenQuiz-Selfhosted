import { API_BASE_URL } from '../config/constants';
import { tokenStore } from './tokenStore';

const NO_BODY = Symbol('no-body');
let refreshPromise = null;

async function attemptRefresh() {
  const refreshToken = tokenStore.getRefreshToken();
  if (!refreshToken) return null;
  if (refreshPromise) return refreshPromise;

  refreshPromise = (async () => {
    try {
      const res = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (!res.ok) {
        tokenStore.clear();
        return null;
      }
      const data = await res.json();
      tokenStore.set(data);
      return data.accessToken;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

async function request(method, path, body = NO_BODY, { auth = true } = {}) {
  const headers = { Accept: 'application/json' };
  let payload;
  if (body !== NO_BODY) {
    headers['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  let token = auth ? tokenStore.getAccessToken() : null;
  if (token) headers.Authorization = `Bearer ${token}`;

  let res = await fetch(`${API_BASE_URL}${path}`, { method, headers, body: payload });

  if (res.status === 401 && auth) {
    const newToken = await attemptRefresh();
    if (newToken) {
      headers.Authorization = `Bearer ${newToken}`;
      res = await fetch(`${API_BASE_URL}${path}`, { method, headers, body: payload });
    }
  }

  if (!res.ok) {
    let err;
    try { err = await res.json(); } catch { err = { title: res.statusText }; }
    const message = err?.detail || err?.title || `HTTP ${res.status}`;
    const error = new Error(message);
    error.status = res.status;
    error.problem = err;
    throw error;
  }

  if (res.status === 204) return null;
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export const api = {
  get: (path, opts) => request('GET', path, NO_BODY, opts),
  post: (path, body, opts) => request('POST', path, body ?? {}, opts),
  put: (path, body, opts) => request('PUT', path, body ?? {}, opts),
  delete: (path, opts) => request('DELETE', path, NO_BODY, opts),
};
