import { useState, useEffect } from 'react';
import { Trash2, Smartphone, Users, AlertTriangle, Copy, QrCode, Loader2, Edit2 } from 'lucide-react';
import { CONTENT_TYPES, POLL_TYPE_KEY } from '../config/constants';
import { pollService } from '../services/pollService';
import { useTranslation } from 'react-i18next';
import QrModal from './QrModal';
import PollForm from './PollForm';

export default function Dashboard({
  onNavigate,
  user,
  showToast,
  isAdmin,
  isAuthorized,
  composer,
  setComposer,
}) {
  const { t } = useTranslation();
  const [polls, setPolls] = useState([]);
  const [loading, setLoading] = useState(true);
  const [qrPoll, setQrPoll] = useState(null);

  const canCreateQuiz = isAdmin || isAuthorized;

  const ct = (key) => t(`contentTypes.${key}.label`);

  const refresh = () =>
    pollService
      .list()
      .then((list) => setPolls(list || []))
      .catch((err) => showToast(err.message || t('dashboard.loadError'), 'error'));

  useEffect(() => { refresh().finally(() => setLoading(false)); }, []);

  const copyToClipboard = async (text) => {
    try { await navigator.clipboard.writeText(text); showToast(t('dashboard.idCopied')); }
    catch { showToast(t('common.copyFailed'), 'error'); }
  };

  const handleDeletePoll = async (poll) => {
    if (!confirm(t('dashboard.confirmDelete', { title: poll.title || t('common.untitled') }))) return;
    try {
      await pollService.remove(poll.id);
      showToast(t('dashboard.deleted'));
      await refresh();
    } catch (err) {
      showToast(err.message || t('dashboard.deleteError'), 'error');
    }
  };

  const handleSaved = async () => {
    setComposer(null);
    await refresh();
  };

  const typeKey = (poll) => POLL_TYPE_KEY[poll.type] || 'contest';

  return (
    <div className="w-full h-full flex flex-col overflow-hidden bg-slate-50">
      <div className="flex-1 overflow-y-auto px-4 sm:px-6 lg:px-8 py-6 custom-scrollbar">
        {qrPoll && <QrModal pollId={qrPoll.id} title={qrPoll.title} onClose={() => setQrPoll(null)} />}

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
              {Object.entries(CONTENT_TYPES).map(([key, cfg]) => (
                <button
                  key={key}
                  onClick={() => setComposer({ contentType: key, pollId: null })}
                  className={`${cfg.classes.solid} text-white px-3 sm:px-4 py-2 rounded-lg flex items-center gap-1.5 sm:gap-2 shadow-lg text-xs sm:text-sm`}
                >
                  <span>{cfg.icon}</span> {ct(key)}
                </button>
              ))}
            </div>
          )}
          {!canCreateQuiz && (
            <div className="bg-amber-50 border border-amber-200 text-amber-700 px-4 py-2 rounded-lg text-sm flex items-center gap-2">
              <AlertTriangle size={16} /> {t('dashboard.notAuthorized')}
            </div>
          )}
        </header>

        {composer ? (
          // Remounting on a new intent is what discards the previous draft.
          <PollForm
            key={`${composer.pollId ?? 'new'}:${composer.contentType}`}
            contentType={composer.contentType}
            poll={polls.find((p) => p.id === composer.pollId) ?? null}
            showToast={showToast}
            onCancel={() => setComposer(null)}
            onSaved={handleSaved}
          />
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
                      {canDelete && <button aria-label={t('common.edit')} onClick={() => setComposer({ contentType: tk, pollId: poll.id })} className="p-2 text-indigo-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-lg"><Edit2 size={16} /></button>}
                    </div>
                  </div>
                  <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded ${cfg.classes.badge} w-fit`}>{ct(tk)}</span>
                  <h3 className="text-base sm:text-lg font-bold mt-2 mb-2 line-clamp-2 text-slate-800">{poll.title || t('common.untitled')}</h3>
                  <p className="text-sm text-slate-500 mb-4 flex items-center gap-2">
                    <span className="bg-slate-100 px-2 py-0.5 rounded text-xs font-bold">{t('dashboard.questionCount', { count: poll.questions?.length || 0 })}</span>
                    {poll.creatorEmail && isAdmin && <span className="text-xs text-slate-400 truncate max-w-[150px]">• {poll.creatorEmail.split('@')[0]}</span>}
                  </p>
                  <div className="mt-auto grid grid-cols-2 gap-2">
                    <button onClick={() => onNavigate('presenter', poll.id)} className="col-span-2 py-2 bg-slate-900 text-white rounded-lg flex items-center justify-center gap-2 font-medium hover:bg-slate-800 text-sm"><Users size={16} /> {t('dashboard.manage')}</button>
                    <button onClick={() => copyToClipboard(poll.id)} className="py-2 border border-slate-200 text-slate-600 rounded-lg flex items-center justify-center gap-2 hover:bg-slate-50 text-xs"><Copy size={14} /> ID</button>
                    <button onClick={() => onNavigate('voter', poll.id)} className="py-2 border border-slate-200 text-slate-600 rounded-lg flex items-center justify-center gap-2 hover:bg-slate-50 text-xs"><Smartphone size={14} /> {t('dashboard.test')}</button>
                  </div>
                </div>
              );
            })}
            {polls.length === 0 && <div className="col-span-full py-10 text-center text-slate-400">{t('dashboard.empty')}</div>}
          </div>
        )}
      </div>
    </div>
  );
}
