import { useState, useEffect, useCallback, useRef } from 'react';
import { CheckCircle2, XCircle, Loader2, Send } from 'lucide-react';
import { CONTENT_TYPES, POLL_TYPE_KEY, POLL_STATUS_KEY, QUESTION_TYPE_KEY } from '../config/constants';
import { pollService } from '../services/pollService';
import { voteService } from '../services/voteService';
import { wordcloudService } from '../services/wordcloudService';
import { reactionService } from '../services/reactionService';
import { realtimeService } from '../services/realtimeService';
import KatexRenderer from './KatexRenderer';
import WaitingRoom from './WaitingRoom';
import WordCloudVoter from './wordcloud/WordCloudVoter';

export default function VoterMode({ pollId, onExit, user, showToast, preloadedPoll = null }) {
  const [step, setStep] = useState('name');
  const [userName, setUserName] = useState('');
  const [poll, setPoll] = useState(preloadedPoll);
  const [currentQIndex, setCurrentQIndex] = useState(preloadedPoll?.currentQuestionIndex ?? 0);
  const [hasVotedForCurrent, setHasVotedForCurrent] = useState(false);
  const [startTime, setStartTime] = useState(Date.now());
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [lastResult, setLastResult] = useState(null);
  const [openAnswer, setOpenAnswer] = useState('');
  const [selectedIndices, setSelectedIndices] = useState([]);
  const [timeLeft, setTimeLeft] = useState(30);
  const [isTimerRunning, setIsTimerRunning] = useState(false);
  const [isRegistered, setIsRegistered] = useState(false);
  const votedQuestionsRef = useRef(new Set());

  useEffect(() => {
    const saved = localStorage.getItem('voterName');
    if (saved) setUserName(saved);
  }, []);

  // Initial load + SignalR subscription
  useEffect(() => {
    if (!pollId) return;
    let active = true;

    pollService.get(pollId).then((p) => {
      if (active && p) {
        setPoll(p);
        setCurrentQIndex(p.currentQuestionIndex ?? 0);
        const q = p.questions?.[p.currentQuestionIndex ?? 0];
        if (q) { setTimeLeft(q.timeLimit || 30); setIsTimerRunning(true); }
      }
    }).catch(console.error);

    const unsubP = realtimeService.subscribe(pollId, {
      onPollUpdated: (p) => {
        if (!active) return;
        setPoll(p);
        const newQ = p.currentQuestionIndex ?? 0;
        if (newQ !== currentQIndex) {
          const q = p.questions?.[newQ];
          if (q) { setTimeLeft(q.timeLimit || 30); setIsTimerRunning(true); }
          setCurrentQIndex(newQ);
          const key = `${pollId}_${newQ}`;
          const voted = votedQuestionsRef.current.has(key);
          setHasVotedForCurrent(voted);
          if (!voted) {
            setLastResult(null);
            setStartTime(Date.now());
            setOpenAnswer('');
            setSelectedIndices([]);
          }
        }
      },
    });

    return () => { active = false; unsubP.then((u) => u && u()); };
  }, [pollId]);

  useEffect(() => {
    let interval;
    if (isTimerRunning && timeLeft > 0 && !hasVotedForCurrent) {
      interval = setInterval(() => setTimeLeft((t) => t - 1), 1000);
    } else if (timeLeft === 0) {
      setIsTimerRunning(false);
    }
    return () => clearInterval(interval);
  }, [isTimerRunning, timeLeft, hasVotedForCurrent]);

  const handleStart = useCallback(async (e) => {
    e.preventDefault();
    if (!userName.trim()) return;
    localStorage.setItem('voterName', userName);
    if (!isRegistered) {
      try { await pollService.join(pollId, userName); setIsRegistered(true); }
      catch (err) { console.warn('join failed', err); }
    }
    setStep('vote');
  }, [userName, pollId, isRegistered]);

  const handleVote = useCallback((optionIndex) => {
    const q = poll.questions[currentQIndex];
    if (q.allowMultiple) {
      if (hasVotedForCurrent) return;
      setSelectedIndices((p) => p.includes(optionIndex) ? p.filter((i) => i !== optionIndex) : [...p, optionIndex]);
      return;
    }
    if (isSubmitting || hasVotedForCurrent) return;
    submitVote([optionIndex]);
  }, [poll, currentQIndex, isSubmitting, hasVotedForCurrent]);

  const handleSubmitMulti = () => {
    if (selectedIndices.length === 0 || isSubmitting || hasVotedForCurrent) return;
    submitVote(selectedIndices);
  };

  const submitVote = async (indices) => {
    setIsSubmitting(true);
    const q = poll.questions[currentQIndex];
    const typeKey = POLL_TYPE_KEY[poll.type] || 'contest';
    const typeCfg = CONTENT_TYPES[typeKey] || CONTENT_TYPES.contest;

    try {
      const result = await voteService.submit(pollId, {
        questionIndex: currentQIndex,
        selectedIndices: indices,
        responseTimeMs: Date.now() - startTime,
        userName,
      });
      const key = `${pollId}_${currentQIndex}`;
      votedQuestionsRef.current.add(key);
      setHasVotedForCurrent(true);
      setLastResult(typeCfg.hasCorrectAnswer ? (result.isCorrect ? 'correct' : 'wrong') : 'voted');
    } catch (err) {
      showToast(err.message || 'Oy gönderilemedi', 'error');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenAnswer = async () => {
    if (isSubmitting || hasVotedForCurrent || !openAnswer.trim()) return;
    setIsSubmitting(true);
    try {
      await voteService.submitOpen(pollId, { questionIndex: currentQIndex, answerText: openAnswer.trim(), userName });
      votedQuestionsRef.current.add(`${pollId}_${currentQIndex}`);
      setHasVotedForCurrent(true);
      setLastResult('submitted');
      setOpenAnswer('');
    } catch (err) {
      showToast(err.message || 'Cevap gönderilemedi', 'error');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleWordCloudSubmit = async (terms) => {
    setIsSubmitting(true);
    try {
      await wordcloudService.submit(pollId, { questionIndex: currentQIndex, terms, userName });
      votedQuestionsRef.current.add(`${pollId}_${currentQIndex}`);
      setHasVotedForCurrent(true);
      setLastResult('submitted');
    } catch (err) {
      showToast(err.message || 'Gönderilemedi', 'error');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReaction = (emoji) => reactionService.send(pollId, emoji, userName).catch(() => {});

  if (!poll) return <div className="h-screen flex items-center justify-center"><Loader2 className="animate-spin text-indigo-600" /></div>;

  const statusKey = POLL_STATUS_KEY[poll.status] || 'live';
  if (step === 'vote' && statusKey === 'waiting') {
    return <WaitingRoom poll={poll} participantCount={poll.participantCount || 0} userName={userName} />;
  }

  if (step === 'name') {
    const typeKey = POLL_TYPE_KEY[poll.type] || 'contest';
    const typeCfg = CONTENT_TYPES[typeKey] || CONTENT_TYPES.contest;
    return (
      <div className="min-h-screen bg-slate-50 flex items-center justify-center p-6">
        <div className="bg-white max-w-sm w-full p-8 rounded-3xl shadow-xl text-center">
          <div className="text-5xl mb-4">{typeCfg.icon}</div>
          <span className={`inline-block px-3 py-1 rounded-full text-xs font-bold mb-4 bg-${typeCfg.color}-100 text-${typeCfg.color}-700`}>{typeCfg.label}</span>
          <h2 className="text-2xl font-bold mb-2">{poll.title}</h2>
          <p className="text-slate-500 text-sm mb-6">{poll.questions?.length || 0} soru</p>
          <form onSubmit={handleStart}>
            <input type="text" value={userName} onChange={(e) => setUserName(e.target.value)} className="w-full p-4 border-2 border-slate-200 rounded-xl mb-4 text-center font-bold text-lg focus:border-indigo-500 outline-none" placeholder="Adınız" required />
            <button type="submit" className="w-full py-4 bg-indigo-600 text-white rounded-xl font-bold text-lg">Katıl</button>
          </form>
        </div>
      </div>
    );
  }

  if (hasVotedForCurrent) {
    if (lastResult === 'voted' || lastResult === 'submitted') {
      return (
        <div className="min-h-screen flex flex-col items-center justify-center p-6 text-white text-center bg-indigo-500">
          <div className="bg-white/20 backdrop-blur-md p-10 rounded-3xl shadow-2xl">
            <CheckCircle2 size={64} className="mx-auto mb-4" />
            <h2 className="text-4xl font-black mb-2">{lastResult === 'submitted' ? 'GÖNDERİLDİ!' : 'TEŞEKKÜRLER!'}</h2>
            <div className="flex items-center justify-center gap-2 bg-black/20 px-4 py-2 rounded-full text-sm font-medium animate-pulse">
              <Loader2 size={16} className="animate-spin" /> Sonraki soru için bekle...
            </div>
          </div>
        </div>
      );
    }
    return (
      <div className={`min-h-screen flex flex-col items-center justify-center p-6 text-white text-center ${lastResult === 'correct' ? 'bg-emerald-500' : 'bg-rose-500'}`}>
        <div className="bg-white/20 backdrop-blur-md p-10 rounded-3xl shadow-2xl">
          {lastResult === 'correct' ? <CheckCircle2 size={64} className="mx-auto mb-4" /> : <XCircle size={64} className="mx-auto mb-4" />}
          <h2 className="text-4xl font-black mb-2">{lastResult === 'correct' ? 'DOĞRU!' : 'YANLIŞ'}</h2>
          <div className="flex items-center justify-center gap-2 bg-black/20 px-4 py-2 rounded-full text-sm font-medium animate-pulse">
            <Loader2 size={16} className="animate-spin" /> Sunucu sonraki soruya geçene kadar bekle...
          </div>
        </div>
      </div>
    );
  }

  const q = poll.questions[currentQIndex];
  if (!q) return <div className="h-screen flex items-center justify-center">Yarışma sona erdi.</div>;

  const typeKey = POLL_TYPE_KEY[poll.type] || 'contest';
  const isExam = typeKey === 'exam';
  const qTypeKey = QUESTION_TYPE_KEY[q.questionType] || 'multiple';
  const isOpen = qTypeKey === 'open';
  const isWordCloud = qTypeKey === 'wordcloud';

  return (
    <div className="h-full bg-slate-50 flex flex-col w-full max-w-2xl mx-auto overflow-hidden">
      <div className="p-4 sm:p-6 shrink-0 z-10 bg-slate-50">
        <div className="flex justify-between items-center mb-3">
          <div className="flex items-center gap-2">
            <span className={`px-2 sm:px-3 py-1 rounded-full text-xs font-bold ${isExam ? 'bg-rose-100 text-rose-700' : isWordCloud ? 'bg-sky-100 text-sky-700' : 'bg-indigo-100 text-indigo-700'}`}>
              SORU {currentQIndex + 1}
            </span>
            {isOpen && <span className="bg-amber-100 text-amber-700 px-2 py-0.5 rounded text-[10px] font-bold">AÇIK UÇLU</span>}
            {isWordCloud && <span className="bg-sky-100 text-sky-700 px-2 py-0.5 rounded text-[10px] font-bold">KELİME BULUTU</span>}
            {isExam && q.points && <span className="bg-slate-100 text-slate-600 px-2 py-0.5 rounded text-[10px] font-bold">{q.points} Puan</span>}
          </div>
          <span className="text-red-500 text-xs font-bold animate-pulse">CANLI</span>
        </div>

        <div className="text-lg sm:text-xl font-bold text-slate-900">
          {q.imageUrl && (<div className="mb-4"><img src={q.imageUrl} alt="Soru" className="max-h-48 rounded-lg shadow-sm mx-auto object-contain" /></div>)}
          {isExam && q.text.includes('$') ? <KatexRenderer text={q.text} /> : q.text}
        </div>

        {!hasVotedForCurrent && !isWordCloud && (
          <div className="mt-4 h-2 bg-slate-100 rounded-full overflow-hidden">
            <div className={`h-full transition-all duration-1000 ease-linear ${timeLeft < 10 ? 'bg-red-500' : 'bg-indigo-500'}`} style={{ width: `${(timeLeft / (q.timeLimit || 30)) * 100}%` }}></div>
          </div>
        )}
      </div>

      <div className="flex-1 p-4 sm:p-6 space-y-2 overflow-y-auto custom-scrollbar">
        {isWordCloud ? (
          <WordCloudVoter maxWords={q.maxWords || 3} isSubmitting={isSubmitting} onSubmit={handleWordCloudSubmit} />
        ) : isOpen ? (
          <div className="space-y-4">
            <textarea
              value={openAnswer}
              onChange={(e) => setOpenAnswer(e.target.value)}
              placeholder="Cevabınızı yazın..."
              rows={6}
              className="w-full p-4 rounded-xl border-2 border-slate-200 bg-white text-base text-slate-700 focus:border-rose-400 outline-none resize-none"
            />
            <button onClick={handleOpenAnswer} disabled={isSubmitting || !openAnswer.trim()} className="w-full py-4 bg-rose-600 text-white rounded-xl font-bold flex items-center justify-center gap-2 disabled:opacity-50">
              {isSubmitting ? <Loader2 className="animate-spin" size={20} /> : <Send size={20} />}
              Cevabı Gönder
            </button>
          </div>
        ) : (
          (q.options || []).map((opt, idx) => (
            <button
              key={opt.id || idx}
              onClick={() => handleVote(idx)}
              disabled={isSubmitting || (hasVotedForCurrent && !q.allowMultiple)}
              className={`w-full p-3 sm:p-4 rounded-xl border-2 text-left font-bold text-base sm:text-lg transition-all ${
                selectedIndices.includes(idx)
                  ? 'border-indigo-600 bg-indigo-50 text-indigo-700 ring-2 ring-indigo-200'
                  : 'border-slate-200 bg-white text-slate-700 hover:border-indigo-500'
              }`}
            >
              {isExam && opt.text.includes('$') ? <KatexRenderer text={opt.text} /> : opt.text}
            </button>
          ))
        )}

        {q.allowMultiple && !hasVotedForCurrent && !isOpen && !isWordCloud && (
          <button onClick={handleSubmitMulti} disabled={selectedIndices.length === 0 || isSubmitting} className="w-full py-4 bg-indigo-600 text-white rounded-xl font-bold flex items-center justify-center gap-2 disabled:opacity-50 mt-4">
            {isSubmitting ? <Loader2 className="animate-spin" size={20} /> : <Send size={20} />}
            Seçimlerimi Gönder ({selectedIndices.length})
          </button>
        )}
      </div>

      <div className="fixed bottom-4 right-4 flex flex-col gap-2 z-40">
        {['❤️', '👍', '🎉', '😂'].map((e) => (
          <button key={e} onClick={() => handleReaction(e)} className="w-12 h-12 bg-white rounded-full shadow-lg flex items-center justify-center text-2xl hover:scale-110 transition-transform border border-slate-100">
            {e}
          </button>
        ))}
      </div>
    </div>
  );
}
