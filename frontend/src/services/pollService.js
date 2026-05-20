import { api } from './apiClient';

export const pollService = {
  list: () => api.get('/api/polls'),
  get: (id) => api.get(`/api/polls/${id}`, { auth: false }),
  create: (data) => api.post('/api/polls', data),
  update: (id, data) => api.put(`/api/polls/${id}`, data),
  remove: (id) => api.delete(`/api/polls/${id}`),
  activate: (id) => api.post(`/api/polls/${id}/activate`),
  nextQuestion: (id) => api.post(`/api/polls/${id}/next-question`),
  prevQuestion: (id) => api.post(`/api/polls/${id}/prev-question`),
  end: (id) => api.post(`/api/polls/${id}/end`),
  join: (id, userName) => api.post(`/api/polls/${id}/join`, { userName }, { auth: false }),
};
