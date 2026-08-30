import { api } from './apiClient';
import { entitlementStore } from './entitlementStore';

export const entitlementService = {
  async me() {
    const entitlements = await api.get('/api/billing/me');
    entitlementStore.set(entitlements);
    return entitlements;
  },
  /** Anonymous: the pricing page needs this before sign-in. */
  plans: () => api.get('/api/billing/plans', { auth: false }),
};
