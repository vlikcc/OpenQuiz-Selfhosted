import { useState, useEffect } from 'react';
import { Plus, Trash2, Smartphone, XCircle, Users, AlertTriangle, Copy, QrCode, Upload, Loader2, BookOpen, Edit2, Image as ImageIcon, Timer } from 'lucide-react';
import { CONTENT_TYPES, POLL_TYPE_KEY, POLL_TYPE_VALUE, QUESTION_TYPE_VALUE, QUESTION_TYPE_KEY } from '../config/constants';
import { pollService } from '../services/pollService';
import QrModal from './QrModal';
import KatexRenderer, { KatexEditor, KATEX_EXAMPLES, MARKDOWN_EXAMPLES } from './KatexRenderer';
import FileImport from './FileImport';

const blankMultiple = () => ({ text: '', options: ['', ''], correctIndex: 0, questionType: 'multiple', correctAnswer: '', image: '', timeLimit: 30 });
const blankOpen = () => ({ text: '', questionType: 'open', correctAnswer: '', points: 10, image: '', timeLimit: 30 });
const blankWordCloud = () => ({ text: '', questionType: 'wordcloud', image: '', timeLimit: 60, maxWords: 3 });

export default function Dashboard({ onNavigate, user, showToast, isAdmin, isAuthorized }) {
  const [polls, setPolls] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [editingPollId, setEditingPollId] = useState(null);
  const [qrPoll, setQrPoll] = useState(null);
  const [showFileImport, setShowFileImport] = useState(false);

  const canCreateQuiz = isAdmin || isAuthorized;

  const [contentType, setContentType] = useState('contest');
  const [quizTitle, setQuizTitle] = useState('');
  const [questions, setQuestions] = useState([blankMultiple()]);
  const [showKatexHelp, setShowKatexHelp] = useState(false);

  const refresh = async () => {
    setLoading(true);
    try {
      const list = await pollService.list();
      setPolls(list || []);
    } catch (err) {
      showToast(err.message || 'Yarışmalar yüklenemedi', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { refresh(); }, []);

  const handleImportQuestions = (importedQuestions) => {
    setQuestions(importedQuestions.map((q) => ({
      text: q.text,
      options: q.options,
      correctIndex: q.correctIndex || 0,
      questionType: 'multiple',
      image: '',
      timeLimit: 30,
    })));
    showToast(`${importedQuestions.length} soru içe aktarıldı!`);
  };

  const copyToClipboard = async (text) => {
    try { await navigator.clipboard.writeText(text); showToast('ID Kopyalandı'); }
    catch { showToast('Kopyalanamadı', 'error'); }
  };

  const addQuestion = (qType = 'multiple') => {
    if (qType === 'open') setQuestions((qs) => [...qs, blankOpen()]);
    else if (qType === 'wordcloud') setQuestions((qs) => [...qs, blankWordCloud()]);
    else setQuestions((qs) => [...qs, blankMultiple()]);
  };

  const updateQuestionType = (index, qType) => {
    setQuestions((qs) => qs.map((q, i) => {
      if (i !== index) return q;
      if (qType === 'open') return { ...blankOpen(), text: q.text, image: q.image, timeLimit: q.timeLimit };
      if (qType === 'wordcloud') return { ...blankWordCloud(), text: q.text, image: q.image, timeLimit: q.timeLimit };
      return { ...blankMultiple(), text: q.text, image: q.image, timeLimit: q.timeLimit };
    }));
  };

  const updateField = (index, field, val) =>
    setQuestions((qs) => qs.map((q, i) => (i === index ? { ...q, [field]: val } : q)));

  const updateOption = (qIndex, oIndex, val) =>
    setQuestions((qs) => qs.map((q, i) => i === qIndex ? { ...q, options: q.options.map((o, j) => (j === oIndex ? val : o)) } : q));

  const addOption = (qIndex) =>
    setQuestions((qs) => qs.map((q, i) => (i === qIndex ? { ...q, options: [...q.options, ''] } : q)));

  const setCorrectOption = (qIndex, oIndex) =>
    setQuestions((qs) => qs.map((q, i) => (i === qIndex ? { ...q, correctIndex: oIndex } : q)));

  const removeQuestion = (index) => {
    if (questions.length === 1) return;
    setQuestions((qs) => qs.filter((_, i) => i !== index));
  };

  const resetForm = () => {
    setIsCreating(false);
    setEditingPollId(null);
    setContentType('contest');
    setQuizTitle('');
    setQuestions([blankMultiple()]);
  };

  const handleCreateQuiz = async (e) => {
    e.preventDefault();
    const typeConfig = CONTENT_TYPES[contentType];
    if (!quizTitle.trim()) return showToast('Başlık giriniz', 'error');

    for (const q of questions) {
      if (!q.text.trim()) return showToast('Tüm soru metinlerini doldurunuz', 'error');
      if (q.questionType === 'multiple' && (q.options || []).some((o) => !o.trim()))
        return showToast('Çoktan seçmeli sorularda tüm seçenekleri doldurunuz', 'error');
    }

    const payloadQuestions = questions.map((q, idx) => ({
      orderIndex: idx,
      text: q.text,
      imageUrl: q.image || '',
      timeLimit: q.timeLimit || 30,
      questionType: QUESTION_TYPE_VALUE[q.questionType] || QUESTION_TYPE_VALUE.multiple,
      allowMultiple: !!q.allowMultiple,
      correctOptionIndex: typeConfig.hasCorrectAnswer && q.questionType === 'multiple' ? parseInt(q.correctIndex || 0, 10) : null,
      correctAnswer: q.correctAnswer || null,
      points: q.points || 10,
      maxWords: q.questionType === 'wordcloud' ? (q.maxWords || 3) : null,
      wordCloudConfig: null,
      options: q.questionType === 'multiple'
        ? (q.options || []).map((optText, optIdx) => ({ orderIndex: optIdx, text: optText }))
        : [],
    }));

    const payload = {
      title: quizTitle,
      type: POLL_TYPE_VALUE[contentType],
      questions: payloadQuestions,
    };

    try {
      if (editingPollId) {
        await pollService.update(editingPollId, payload);
        showToast(`${typeConfig.label} güncellendi!`);
      } else {
        await pollService.create(payload);
        showToast(`${typeConfig.label} oluşturuldu!`);
      }
      resetForm();
      await refresh();
    } catch (err) {
      showToast(err.message || 'Hata oluştu', 'error');
    }
  };

  const handleEditPoll = (poll) => {
    setIsCreating(true);
    setEditingPollId(poll.id);
    const typeKey = POLL_TYPE_KEY[poll.type] || 'contest';
    setContentType(typeKey);
    setQuizTitle(poll.title);

    const formatted = (poll.questions || []).map((q) => {
      const qt = QUESTION_TYPE_KEY[q.questionType] || 'multiple';
      if (qt === 'open') return { text: q.text, questionType: 'open', correctAnswer: q.correctAnswer || '', points: q.points || 10, image: q.imageUrl || '', timeLimit: q.timeLimit || 30 };
      if (qt === 'wordcloud') return { text: q.text, questionType: 'wordcloud', image: q.imageUrl || '', timeLimit: q.timeLimit || 60, maxWords: q.maxWords || 3 };
      return {
        text: q.text,
        questionType: 'multiple',
        allowMultiple: q.allowMultiple || false,
        options: (q.options || []).map((o) => o.text),
        correctIndex: q.correctOptionIndex ?? 0,
        points: q.points || 10,
        image: q.imageUrl || '',
        timeLimit: q.timeLimit || 30,
      };
    });
    setQuestions(formatted.length ? formatted : [blankMultiple()]);
  };

  const handleDeletePoll = async (poll) => {
    if (!confirm(`"${poll.title || 'Bu yarışma'}" silinecek. Emin misiniz?`)) return;
    try {
      await pollService.remove(poll.id);
      showToast('Yarışma silindi');
      await refresh();
    } catch (err) {
      showToast(err.message || 'Silme hatası', 'error');
    }
  };

  const typeKey = (poll) => POLL_TYPE_KEY[poll.type] || 'contest';

  return (
    <div className="w-full h-full flex flex-col overflow-hidden bg-slate-50">
      <div className="flex-1 overflow-y-auto px-4 sm:px-6 lg:px-8 py-6 custom-scrollbar">
        {qrPoll && <QrModal pollId={qrPoll.id} title={qrPoll.title} onClose={() => setQrPoll(null)} />}
        {showFileImport && <FileImport onImport={handleImportQuestions} onClose={() => setShowFileImport(false)} />}

        <header className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-8">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">{isAdmin ? 'Tüm Yarışmalar' : 'Yarışmalarım'}</h1>
            <p className="text-slate-500 text-sm sm:text-base">
              {isAdmin ? 'Tüm yarışmaları görüntüleyebilir ve yönetebilirsiniz.' :
                canCreateQuiz ? 'Kendi yarışmalarınızı oluşturun ve yönetin.' : 'Yarışmalara katılabilir ve sonuçları görebilirsiniz.'}
            </p>
          </div>
          {!isCreating && canCreateQuiz && (
            <div className="flex flex-wrap gap-2 sm:gap-3">
              {Object.entries(CONTENT_TYPES).map(([key, cfg]) => (
                <button
                  key={key}
                  onClick={() => {
                    setContentType(key);
                    if (cfg.questionType === 'open') setQuestions([blankOpen()]);
                    else if (cfg.questionType === 'wordcloud') setQuestions([blankWordCloud()]);
                    else setQuestions([blankMultiple()]);
                    setIsCreating(true);
                  }}
                  className={`bg-${cfg.color}-600 hover:bg-${cfg.color}-700 text-white px-3 sm:px-4 py-2 rounded-lg flex items-center gap-1.5 sm:gap-2 shadow-lg text-xs sm:text-sm`}
                >
                  <span>{cfg.icon}</span> {cfg.label}
                </button>
              ))}
            </div>
          )}
          {!canCreateQuiz && (
            <div className="bg-amber-50 border border-amber-200 text-amber-700 px-4 py-2 rounded-lg text-sm flex items-center gap-2">
              <AlertTriangle size={16} /> İçerik oluşturma yetkiniz yok
            </div>
          )}
        </header>

        {isCreating ? (
          <div className="bg-white p-4 sm:p-6 lg:p-8 rounded-2xl shadow-xl border border-indigo-50 max-w-6xl mx-auto">
            <div className="flex justify-between items-center mb-4 sm:mb-6 border-b pb-4">
              <h2 className="text-lg sm:text-xl font-bold text-slate-800">
                {CONTENT_TYPES[contentType].icon} {CONTENT_TYPES[contentType].label} {editingPollId ? 'Düzenle' : 'Oluştur'}
              </h2>
              <button onClick={resetForm} className="text-slate-400 hover:text-slate-600"><XCircle size={22} /></button>
            </div>

            <div className="mb-6">
              <label className="block text-sm font-bold text-slate-700 mb-2">Başlık</label>
              <input
                type="text"
                value={quizTitle}
                onChange={(e) => setQuizTitle(e.target.value)}
                className="w-full p-3 sm:p-4 text-base sm:text-lg border-2 border-slate-200 rounded-xl focus:border-indigo-500 outline-none"
                placeholder={contentType === 'wordcloud' ? 'Örn: En sevdiğin meyve?' : `Örn: Genel Kültür Yarışması`}
              />
            </div>

            {contentType === 'exam' && (
              <div className="mb-6">
                <button
                  type="button"
                  onClick={() => setShowKatexHelp(!showKatexHelp)}
                  className="flex items-center gap-2 text-rose-600 hover:text-rose-700 font-medium text-sm"
                >
                  <BookOpen size={16} /> {showKatexHelp ? 'Format Yardımını Gizle' : 'Format Yardımı'}
                </button>
                {showKatexHelp && (
                  <div className="mt-3 space-y-4">
                    <div className="bg-rose-50 border border-rose-200 rounded-xl p-4">
                      <h4 className="font-bold text-rose-800 mb-3">📐 KaTeX</h4>
                      <div className="grid grid-cols-2 sm:grid-cols-4 md:grid-cols-7 gap-2">
                        {KATEX_EXAMPLES.map((ex, i) => (
                          <div key={i} className="bg-white p-2 rounded-lg border border-rose-100 text-center">
                            <div className="text-lg mb-1"><KatexRenderer text={`$${ex.code}$`} /></div>
                            <code className="text-[10px] text-slate-500 block truncate">${ex.code}$</code>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                )}
              </div>
            )}

            <div className="space-y-8">
              {questions.map((q, qIndex) => (
                <div key={qIndex} className="bg-slate-50 p-6 rounded-xl border border-slate-200 relative">
                  <div className="absolute top-4 right-4 flex gap-2">
                    {contentType === 'exam' && q.questionType !== 'open' && (
                      <div className="flex items-center gap-1 bg-white rounded-lg px-2 py-1 border border-slate-200">
                        <span className="text-xs text-slate-500">Puan:</span>
                        <input type="number" value={q.points || 10} onChange={(e) => updateField(qIndex, 'points', parseInt(e.target.value, 10) || 10)} className="w-12 text-center text-sm font-bold text-rose-600 outline-none" min="1" />
                      </div>
                    )}
                    <button onClick={() => removeQuestion(qIndex)} className="text-red-400 hover:text-red-600 p-2"><Trash2 size={20} /></button>
                  </div>

                  {contentType === 'exam' && (
                    <div className="flex gap-2 mb-4">
                      <button type="button" onClick={() => updateQuestionType(qIndex, 'multiple')} className={`px-4 py-2 rounded-lg text-sm font-medium ${q.questionType !== 'open' ? 'bg-rose-600 text-white' : 'bg-white border border-slate-200 text-slate-600'}`}>📋 Çoktan Seçmeli</button>
                      <button type="button" onClick={() => updateQuestionType(qIndex, 'open')} className={`px-4 py-2 rounded-lg text-sm font-medium ${q.questionType === 'open' ? 'bg-rose-600 text-white' : 'bg-white border border-slate-200 text-slate-600'}`}>✏️ Açık Uçlu</button>
                    </div>
                  )}

                  <div className="mb-4">
                    <div className="flex gap-4 mb-3">
                      <div className="flex-1">
                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1 flex items-center gap-1"><ImageIcon size={14} /> Görsel URL (Opsiyonel)</label>
                        <input type="text" value={q.image || ''} onChange={(e) => updateField(qIndex, 'image', e.target.value)} className="w-full p-2 text-sm bg-white border border-slate-300 rounded-lg outline-none focus:border-indigo-500" placeholder="https://..." />
                      </div>
                      <div className="w-24">
                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1 flex items-center gap-1"><Timer size={14} /> Süre (sn)</label>
                        <input type="number" value={q.timeLimit || 30} onChange={(e) => updateField(qIndex, 'timeLimit', parseInt(e.target.value, 10) || 30)} className="w-full p-2 text-sm bg-white border border-slate-300 rounded-lg outline-none focus:border-indigo-500 text-center font-bold" min="5" max="600" />
                      </div>
                      {q.questionType === 'wordcloud' && (
                        <div className="w-24">
                          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Max Kelime</label>
                          <input type="number" value={q.maxWords || 3} onChange={(e) => updateField(qIndex, 'maxWords', parseInt(e.target.value, 10) || 3)} className="w-full p-2 text-sm bg-white border border-slate-300 rounded-lg outline-none focus:border-sky-500 text-center font-bold" min="1" max="20" />
                        </div>
                      )}
                    </div>

                    <label className="block text-xs font-bold text-indigo-600 uppercase mb-1">Soru {qIndex + 1}</label>
                    {contentType === 'exam' ? (
                      <KatexEditor value={q.text} onChange={(val) => updateField(qIndex, 'text', val)} placeholder="Soru metnini giriniz... ($formül$)" rows={3} />
                    ) : (
                      <input type="text" value={q.text} onChange={(e) => updateField(qIndex, 'text', e.target.value)} className="w-full p-3 bg-white border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-200 outline-none font-medium" placeholder="Soru metnini giriniz..." />
                    )}
                  </div>

                  {q.questionType === 'open' ? (
                    <div className="space-y-4">
                      <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Beklenen Cevap (Opsiyonel)</label>
                      <KatexEditor value={q.correctAnswer || ''} onChange={(val) => updateField(qIndex, 'correctAnswer', val)} placeholder="Anahtar kelimeler veya örnek cevap (opsiyonel)" rows={2} />
                    </div>
                  ) : q.questionType === 'wordcloud' ? (
                    <div className="text-sm text-slate-500 italic bg-sky-50 border border-sky-200 rounded-lg p-3">
                      ☁️ Katılımcılar bu soruya kelime yazacak (maksimum {q.maxWords || 3} kelime). Sonuçlar canlı kelime bulutunda gösterilecek.
                    </div>
                  ) : (
                    <div className="space-y-4">
                      {contentType === 'survey' && (
                        <div className="flex items-center gap-2 mb-2 bg-emerald-50 p-3 rounded-lg border border-emerald-100">
                          <input type="checkbox" id={`multi-${qIndex}`} checked={q.allowMultiple || false} onChange={(e) => updateField(qIndex, 'allowMultiple', e.target.checked)} className="w-5 h-5 accent-emerald-600 cursor-pointer" />
                          <label htmlFor={`multi-${qIndex}`} className="text-sm font-medium text-emerald-800 cursor-pointer">Birden fazla seçenek seçilebilir</label>
                        </div>
                      )}

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {q.options && q.options.map((opt, oIndex) => (
                          <div key={oIndex} className={`flex items-center gap-2 p-2 rounded-lg border-2 bg-white ${CONTENT_TYPES[contentType].hasCorrectAnswer && q.correctIndex === oIndex ? 'border-emerald-500 ring-1 ring-emerald-500' : 'border-transparent'}`}>
                            {CONTENT_TYPES[contentType].hasCorrectAnswer ? (
                              <input type="radio" name={`correct-${qIndex}`} checked={q.correctIndex === oIndex} onChange={() => setCorrectOption(qIndex, oIndex)} className="w-5 h-5 accent-emerald-600 cursor-pointer" />
                            ) : (
                              <div className="w-5 h-5 rounded-full bg-slate-200 flex items-center justify-center text-xs text-slate-500 font-bold">{oIndex + 1}</div>
                            )}
                            <input type="text" value={opt} onChange={(e) => updateOption(qIndex, oIndex, e.target.value)} className="w-full p-2 outline-none text-sm" placeholder={`Seçenek ${oIndex + 1}`} />
                          </div>
                        ))}
                        <button type="button" onClick={() => addOption(qIndex)} className="flex items-center justify-center gap-2 p-2 border-2 border-dashed border-slate-300 rounded-lg text-slate-500 hover:border-indigo-400 hover:text-indigo-600 text-sm font-medium">
                          <Plus size={16} /> Seçenek Ekle
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>

            <div className="mt-8 flex flex-col md:flex-row gap-4 justify-between items-center border-t pt-6">
              <div className="flex flex-wrap gap-3">
                {contentType === 'exam' ? (
                  <>
                    <button type="button" onClick={() => addQuestion('multiple')} className="py-3 px-6 bg-slate-100 text-slate-700 rounded-xl font-bold flex items-center gap-2"><Plus size={20} /> 📋 Çoktan Seçmeli</button>
                    <button type="button" onClick={() => addQuestion('open')} className="py-3 px-6 bg-rose-100 text-rose-700 rounded-xl font-bold flex items-center gap-2"><Plus size={20} /> ✏️ Açık Uçlu</button>
                  </>
                ) : (
                  <button type="button" onClick={() => addQuestion(CONTENT_TYPES[contentType].questionType === 'wordcloud' ? 'wordcloud' : 'multiple')} className="py-3 px-6 bg-slate-100 text-slate-700 rounded-xl font-bold flex items-center gap-2">
                    <Plus size={20} /> Soru Ekle
                  </button>
                )}
                {contentType !== 'wordcloud' && (
                  <button type="button" onClick={() => setShowFileImport(true)} className="py-3 px-6 bg-emerald-100 text-emerald-700 rounded-xl font-bold flex items-center gap-2"><Upload size={20} /> Dosyadan Yükle</button>
                )}
              </div>
              <div className="flex gap-4">
                <button type="button" onClick={resetForm} className="py-3 px-6 text-slate-500 hover:bg-slate-50 rounded-xl font-medium">İptal</button>
                <button onClick={handleCreateQuiz} className={`py-3 px-8 text-white rounded-xl font-bold shadow-lg bg-${CONTENT_TYPES[contentType].color}-600 hover:bg-${CONTENT_TYPES[contentType].color}-700`}>
                  {CONTENT_TYPES[contentType].icon} {editingPollId ? 'Güncelle' : 'Kaydet'}
                </button>
              </div>
            </div>
          </div>
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
                    <div className={`bg-${cfg.color}-50 text-${cfg.color}-700 p-2 rounded-lg`}><span className="text-lg">{cfg.icon}</span></div>
                    <div className="flex gap-1">
                      <button onClick={() => setQrPoll(poll)} className="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg"><QrCode size={16} /></button>
                      {canDelete && <button onClick={() => handleDeletePoll(poll)} className="p-2 text-red-400 hover:text-red-600 hover:bg-red-50 rounded-lg"><Trash2 size={16} /></button>}
                      {canDelete && <button onClick={() => handleEditPoll(poll)} className="p-2 text-indigo-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-lg"><Edit2 size={16} /></button>}
                    </div>
                  </div>
                  <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded bg-${cfg.color}-100 text-${cfg.color}-700 w-fit`}>{cfg.label}</span>
                  <h3 className="text-base sm:text-lg font-bold mt-2 mb-2 line-clamp-2 text-slate-800">{poll.title || 'Başlıksız'}</h3>
                  <p className="text-sm text-slate-500 mb-4 flex items-center gap-2">
                    <span className="bg-slate-100 px-2 py-0.5 rounded text-xs font-bold">{poll.questions?.length || 0} Soru</span>
                    {poll.creatorEmail && isAdmin && <span className="text-xs text-slate-400 truncate max-w-[150px]">• {poll.creatorEmail.split('@')[0]}</span>}
                  </p>
                  <div className="mt-auto grid grid-cols-2 gap-2">
                    <button onClick={() => onNavigate('presenter', poll.id)} className="col-span-2 py-2 bg-slate-900 text-white rounded-lg flex items-center justify-center gap-2 font-medium hover:bg-slate-800 text-sm"><Users size={16} /> Yönet ve Sun</button>
                    <button onClick={() => copyToClipboard(poll.id)} className="py-2 border border-slate-200 text-slate-600 rounded-lg flex items-center justify-center gap-2 hover:bg-slate-50 text-xs"><Copy size={14} /> ID</button>
                    <button onClick={() => onNavigate('voter', poll.id)} className="py-2 border border-slate-200 text-slate-600 rounded-lg flex items-center justify-center gap-2 hover:bg-slate-50 text-xs"><Smartphone size={14} /> Test</button>
                  </div>
                </div>
              );
            })}
            {polls.length === 0 && <div className="col-span-full py-10 text-center text-slate-400">Henüz yarışma yok.</div>}
          </div>
        )}
      </div>
    </div>
  );
}
