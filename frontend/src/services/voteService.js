import { api } from './apiClient';
import { fetchAllPages, pageQuery } from './paging';

export const voteService = {
  submit: (pollId, payload) => api.post(`/api/polls/${pollId}/votes`, payload, { auth: false }),
  submitOpen: (pollId, payload) => api.post(`/api/polls/${pollId}/open-answers`, payload, { auth: false }),
  list: (pollId, paging) => api.get(`/api/polls/${pollId}/votes?${pageQuery(paging)}`),
  listOpen: (pollId, paging) => api.get(`/api/polls/${pollId}/open-answers?${pageQuery(paging)}`),
  aggregates: (pollId) => api.get(`/api/polls/${pollId}/aggregates`, { auth: false }),

  /** Every open answer, for the presenter and the exports. */
  allOpen: (pollId) => fetchAllPages((paging) => voteService.listOpen(pollId, paging)),
};
