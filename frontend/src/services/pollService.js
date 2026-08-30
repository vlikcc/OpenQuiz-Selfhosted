import { api } from './apiClient';
import { pageQuery } from './paging';

export const pollService = {
  /** Paged summaries: `{ items, page, pageSize, totalCount, hasMore }`. */
  list: (paging) => api.get(`/api/polls?${pageQuery(paging)}`),
  get: (id) => api.get(`/api/polls/${id}`, { auth: false }),
  create: (data) => api.post('/api/polls', data),
  update: (id, data) => api.put(`/api/polls/${id}`, data),

  /** A fresh, unplayed copy of a poll, for running it with another group. */
  duplicate: (id, title) => api.post(`/api/polls/${id}/duplicate`, { title }),
  remove: (id) => api.delete(`/api/polls/${id}`),
  activate: (id) => api.post(`/api/polls/${id}/activate`),
  nextQuestion: (id) => api.post(`/api/polls/${id}/next-question`),
  prevQuestion: (id) => api.post(`/api/polls/${id}/prev-question`),
  end: (id) => api.post(`/api/polls/${id}/end`),
  join: (id, userName) => api.post(`/api/polls/${id}/join`, { userName }, { auth: false }),

  /** Owner-only dump for CSV/Excel/PDF. Gated on reports.export (402). */
  report: (id) => api.get(`/api/polls/${id}/report`),
};
