import { api } from './apiClient';

export const reactionService = {
  send: (pollId, emoji, sender) =>
    api.post(`/api/polls/${pollId}/reactions`, { emoji, sender }, { auth: false }),
};
