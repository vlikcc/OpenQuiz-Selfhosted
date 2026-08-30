import { CreditCard } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useEntitlements } from '../hooks/useEntitlements';
import { EntitlementKeys } from '../config/entitlementKeys';
import { PLAN_LABEL_I18N } from '../config/planLabels';

function UsageBar({ used, limit, label, t }) {
  const isUnlimited = limit === EntitlementKeys.Unlimited;
  const pct = isUnlimited || limit <= 0 ? 0 : Math.min(100, Math.round((used / limit) * 100));
  return (
    <div>
      <div className="flex justify-between text-xs text-slate-500 mb-1">
        <span>{label}</span>
        <span>{isUnlimited ? t('billing.unlimited') : `${used} / ${limit}`}</span>
      </div>
      {!isUnlimited && (
        <div className="h-1.5 bg-slate-100 rounded-full overflow-hidden">
          <div className="h-full bg-indigo-500 rounded-full" style={{ width: `${pct}%` }} />
        </div>
      )}
    </div>
  );
}

export default function BillingSection() {
  const { t } = useTranslation();
  const { entitlements, plan, usage, loading } = useEntitlements();

  if (loading && !entitlements) return null;
  if (!entitlements) return null;

  const planLabel = t(PLAN_LABEL_I18N[plan] || plan);
  const activePollsLimit = entitlements.values?.[EntitlementKeys.LimitActivePolls] ?? 0;
  const sessionsLimit = entitlements.values?.[EntitlementKeys.LimitSessionsPerMonth] ?? 0;
  const activePollsUsed = usage?.[EntitlementKeys.LimitActivePolls] ?? 0;
  const sessionsUsed = usage?.[EntitlementKeys.LimitSessionsPerMonth] ?? 0;

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-4 space-y-4">
      <div className="flex items-center gap-4">
        <div className="w-10 h-10 bg-indigo-100 rounded-xl flex items-center justify-center">
          <CreditCard size={20} className="text-indigo-600" />
        </div>
        <div className="flex-1">
          <div className="text-xs text-slate-500">{t('billing.currentPlan')}</div>
          <div className="font-semibold text-slate-800">{planLabel}</div>
        </div>
      </div>

      <div className="space-y-3">
        <UsageBar used={activePollsUsed} limit={activePollsLimit} label={t('billing.usageActivePolls')} t={t} />
        <UsageBar used={sessionsUsed} limit={sessionsLimit} label={t('billing.usageSessionsPerMonth')} t={t} />
      </div>

      <p className="text-xs text-slate-400">{t('billing.manageHint')}</p>
    </div>
  );
}
