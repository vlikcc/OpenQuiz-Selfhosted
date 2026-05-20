import { api } from './apiClient';

export const voteService = {
  submit: (pollId, payload) => api.post(`/api/polls/${pollId}/votes`, payload, { auth: false }),
  submitOpen: (pollId, payload) => api.post(`/api/polls/${pollId}/open-answers`, payload, { auth: false }),
  list: (pollId) => api.get(`/api/polls/${pollId}/votes`),
  listOpen: (pollId) => api.get(`/api/polls/${pollId}/open-answers`),
  aggregates: (pollId) => api.get(`/api/polls/${pollId}/aggregates`, { auth: false }),
};
