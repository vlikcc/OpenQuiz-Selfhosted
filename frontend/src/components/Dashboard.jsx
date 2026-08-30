import { Suspense, lazy, useCallback, useState, useEffect } from 'react';
import { Trash2, Smartphone, Users, AlertTriangle, Copy, CopyPlus, QrCode, Loader2, Edit2, BarChart3, Lock, CreditCard } from 'lucide-react';
import { CONTENT_TYPES, POLL_TYPE_KEY } from '../config/constants';
import { EntitlementKeys } from '../config/entitlementKeys';
import { PLAN_LABEL_I18N } from '../config/planLabels';
import { pollService } from '../services/pollService';
import { useTranslation } from 'react-i18next';
import QrModal from './QrModal';
import LoadingPanel from './LoadingPanel';
import UpgradeModal from './UpgradeModal';

// Only these two content types are plan-gated today (see EntitlementKeys on
// the backend) — Contest/Survey/Quiz have no feature key and are always on.
const GATED_CONTENT_TYPES = {
  exam: EntitlementKeys.ContentExam,
  wordcloud: EntitlementKeys.ContentWordCloud,
};

// The editor reaches the spreadsheet parsers and the KaTeX preview; the poll
// list itself needs none of that.
const PollForm = lazy(() => import('./PollForm'));
// The report screen carries the charts and, behind its export buttons, the
// spreadsheet and PDF writers.
const ResultsAnalysis = lazy(() => import('./ResultsAnalysis'));

export default function Dashboard({
  onNavigate,
  user,
  showToast,
  isAdmin,
  isAuthorized,
  composer,
  setComposer,
  can = () => true,
  plan = null,
}) {
  const { t } = useTranslation();
  const [polls, setPolls] = useState([]);
  const [loadedPage, setLoadedPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [qrPoll, setQrPoll] = useState(null);
  // The list only carries summaries, so editing fetches the questions itself.
  const [editing, setEditing] = useState(null);
  // The report screen needs the questions too: { id, poll } while it loads.
  const [reporting, setReporting] = useState(null);
  // A locked content type's featureKey, or null. Clicking a locked button
  // never reaches the server, so this is raised locally rather than through
  // apiClient's 402 handler.
  const [upgradePrompt, setUpgradePrompt] = useState(null);

  const canCreateQuiz = isAdmin || isAuthorized;

  const ct = (key) => t(`contentTypes.${key}.label`);

  const loadPage = useCallback(
    (page) =>
      pollService
        .list({ page })
        .then((result) => {
          setPolls((current) => (page === 1 ? result.items : [...current, ...result.items]));
          setLoadedPage(page);
          setHasMore(result.hasMore);
        })
        .catch((err) => showToast(err.message || t('dashboard.loadError'), 'error')),
    [showToast, t],
  );

  useEffect(() => { loadPage(1).finally(() => setLoading(false)); }, [loadPage]);

  useEffect(() => {
    const pollId = composer?.pollId;
    if (!pollId) return undefined;

    let current = true;
    pollService
      .get(pollId)
      .then((poll) => { if (current) setEditing({ pollId, poll }); })
      .catch((err) => showToast(err.message || t('dashboard.loadError'), 'error'));

    return () => { current = false; };
  }, [composer?.pollId, showToast, t]);

  // Tagged with the id it was fetched for, so switching polls shows the loader
  // rather than the previous poll's questions.
  const editingPoll = composer?.pollId && editing?.pollId === composer.pollId ? editing.poll : null;

  const handleLoadMore = () => {
    setLoadingMore(true);
    loadPage(loadedPage + 1).finally(() => setLoadingMore(false));
  };

  const copyToClipboard = async (text) => {
    try { await navigator.clipboard.writeText(text); showToast(t('dashboard.idCopied')); }
    catch { showToast(t('common.copyFailed'), 'error'); }
  };

  const handleDeletePoll = async (poll) => {
    if (!confirm(t('dashboard.confirmDelete', { title: poll.title || t('common.untitled') }))) return;
    try {
      await pollService.remove(poll.id);
      showToast(t('dashboard.deleted'));
      await loadPage(1);
    } catch (err) {
      showToast(err.message || t('dashboard.deleteError'), 'error');
    }
  };

  const handleDuplicate = async (poll) => {
    const title = t('dashboard.copyTitle', { title: poll.title || t('common.untitled') });
    try {
      const copy = await pollService.duplicate(poll.id, title);
      showToast(t('dashboard.duplicated', { title: copy.title }));
      // The list is newest first, so the copy lands at the top.
      await loadPage(1);
    } catch (err) {
      showToast(err.message || t('dashboard.duplicateError'), 'error');
    }
  };

  const handleShowResults = async (poll) => {
    setReporting({ id: poll.id, poll: null });
    try {
      setReporting({ id: poll.id, poll: await pollService.get(poll.id) });
    } catch (err) {
      setReporting(null);
      showToast(err.message || t('dashboard.loadError'), 'error');
    }
  };

  const handleSaved = async () => {
    setComposer(null);
    await loadPage(1);
  };

  const typeKey = (poll) => POLL_TYPE_KEY[poll.type] || 'contest';

  return (
    <div className="w-full h-full flex flex-col overflow-hidden bg-slate-50">
      <div className="flex-1 overflow-y-auto px-4 sm:px-6 lg:px-8 py-6 custom-scrollbar">
        {qrPoll && <QrModal pollId={qrPoll.id} title={qrPoll.title} onClose={() => setQrPoll(null)} />}
        {upgradePrompt && (
          <UpgradeModal
            info={{ code: 'plan_feature_required', featureKey: upgradePrompt }}
            onClose={() => setUpgradePrompt(null)}
          />
        )}

        {reporting && (
          reporting.poll ? (
            <Suspense fallback={<LoadingPanel />}>
              <ResultsAnalysis poll={reporting.poll} pollId={reporting.id} onClose={() => setReporting(null)} />
            </Suspense>
          ) : (
            <LoadingPanel />
          )
        )}

        <header className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-8">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">{isAdmin ? t('dashboard.allPolls') : t('dashboard.myPolls')}</h1>
            <p className="text-slate-500 text-sm sm:text-base">
              {isAdmin ? t('dashboard.adminSubtitle') :
                canCreateQuiz ? t('dashboard.creatorSubtitle') : t('dashboard.voterSubtitle')}
            </p>
          </div>
          {!composer && canCreateQuiz && (
            <div className="flex flex-wrap gap-2 sm:gap-3">
              {Object.entries(CONTENT_TYPES).map(([key, cfg]) => {
                const featureKey = GATED_CONTENT_TYPES[key];
                const locked = featureKey && !can(featureKey);
                return (
                  <button
                    key={key}
                    onClick={() => (locked ? setUpgradePrompt(featureKey) : setComposer({ contentType: key, pollId: null }))}
                    className={`${locked ? 'bg-slate-100 text-slate-400 hover:bg-slate-200' : `${cfg.classes.solid} text-white`} px-3 sm:px-4 py-2 rounded-lg flex items-center gap-1.5 sm:gap-2 shadow-lg text-xs sm:text-sm`}
                  >
                    {locked ? <Lock size={14} /> : <span>{cfg.icon}</span>} {ct(key)}
                  </button>
                );
              })}
            </div>
          )}
          {!canCreateQuiz && (
            <div className="bg-amber-50 border border-amber-200 text-amber-700 px-4 py-2 rounded-lg text-sm flex items-center gap-2">
              <AlertTriangle size={16} /> {t('dashboard.notAuthorized')}
            </div>
          )}
        </header>

        {!composer && canCreateQuiz && plan && plan !== 'unlimited' && (
          <div className="bg-indigo-50 border border-indigo-200 text-indigo-700 px-4 py-2 rounded-lg text-sm flex items-center gap-2 mb-6 w-fit">
            <CreditCard size={16} /> {t('billing.currentPlan')}: {t(PLAN_LABEL_I18N[plan] || plan)}
          </div>
        )}

        {composer ? (
          composer.pollId && !editingPoll ? (
            <LoadingPanel />
          ) : (
            <Suspense fallback={<LoadingPanel />}>
              {/* Remounting on a new intent is what discards the previous draft. */}
              <PollForm
                key={`${composer.pollId ?? 'new'}:${composer.contentType}`}
                contentType={composer.contentType}
                poll={editingPoll}
                showToast={showToast}
                onCancel={() => setComposer(null)}
                onSaved={handleSaved}
              />
            </Suspense>
          )
        ) : loading ? (
          <div className="text-center py-10"><Loader2 className="animate-spin text-indigo-600 mx-auto" /></div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 sm:gap-6">
            {polls.map((poll) => {
              const tk = typeKey(poll);
              const cfg = CONTENT_TYPES[tk] || CONTENT_TYPES.contest;
              const canDelete = isAdmin || poll.creatorEmail === user.email;
              return (
                <div key={poll.id} className="bg-white p-5 rounded-2xl shadow-sm border border-slate-200 hover:shadow-lg flex flex-col group min-h-[220px]">
                  <div className="flex justify-between items-start mb-3">
                    <div className={`${cfg.classes.soft} p-2 rounded-lg`}><span className="text-lg">{cfg.icon}</span></div>
                    <div className="flex gap-1">
                      <button aria-label={t('dashboard.showQr')} onClick={() => setQrPoll(poll)} className="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg"><QrCode size={16} /></button>
                      {canDelete && <button aria-label={t('common.delete')} onClick={() => handleDeletePoll(poll)} className="p-2 text-red-400 hover:text-red-600 hover:bg-red-50 rounded-lg"><Trash2 size={16} /></button>}
                      {canCreateQuiz && <button aria-label={t('dashboard.duplicate')} onClick={() => handleDuplicate(poll)} className="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg"><CopyPlus size={16} /></button>}
                      {canDelete && <button aria-label={t('common.edit')} onClick={() => setComposer({ contentType: tk, pollId: poll.id })} className="p-2 text-indigo-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-lg"><Edit2 size={16} /></button>}
                    </div>
                  </div>
                  <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded ${cfg.classes.badge} w-fit`}>{ct(tk)}</span>
                  <h3 className="text-base sm:text-lg font-bold mt-2 mb-2 line-clamp-2 text-slate-800">{poll.title || t('common.untitled')}</h3>
                  <p className="text-sm text-slate-500 mb-4 flex items-center gap-2">
                    <span className="bg-slate-100 px-2 py-0.5 rounded text-xs font-bold">{t('dashboard.questionCount', { count: poll.questionCount || 0 })}</span>
                    {poll.creatorEmail && isAdmin && <span className="text-xs text-slate-400 truncate max-w-[150px]">• {poll.creatorEmail.split('@')[0]}</span>}
                  </p>
                  <div className="mt-auto grid grid-cols-3 gap-2">
                    <button onClick={() => onNavigate('presenter', poll.id)} className="col-span-3 py-2 bg-slate-900 text-white rounded-lg flex items-center justify-center gap-2 font-medium hover:bg-slate-800 text-sm"><Users size={16} /> {t('dashboard.manage')}</button>
                    <button onClick={() => copyToClipboard(poll.id)} className="py-2 border border-slate-200 text-slate-600 rounded-lg flex items-center justify-center gap-2 hover:bg-slate-50 text-xs"><Copy size={14} /> ID</button>
                    <button onClick={() => onNavigate('voter', poll.id)} className="py-2 border border-slate-200 text-slate-600 rounded-lg flex items-center justify-center gap-2 hover:bg-slate-50 text-xs"><Smartphone size={14} /> {t('dashboard.test')}</button>
                    <button onClick={() => handleShowResults(poll)} className="py-2 border border-slate-200 text-slate-600 rounded-lg flex items-center justify-center gap-2 hover:bg-slate-50 text-xs"><BarChart3 size={14} /> {t('dashboard.results')}</button>
                  </div>
                </div>
              );
            })}
            {polls.length === 0 && <div className="col-span-full py-10 text-center text-slate-400">{t('dashboard.empty')}</div>}
            {hasMore && (
              <div className="col-span-full flex justify-center py-4">
                <button
                  onClick={handleLoadMore}
                  disabled={loadingMore}
                  className="px-6 py-2 border border-slate-300 text-slate-600 rounded-lg font-medium hover:bg-white disabled:opacity-50 flex items-center gap-2"
                >
                  {loadingMore && <Loader2 className="animate-spin" size={16} />}
                  {t('dashboard.loadMore')}
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
