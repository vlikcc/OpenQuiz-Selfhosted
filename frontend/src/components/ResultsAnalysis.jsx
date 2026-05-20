import { useState, useEffect, useMemo } from 'react';
import { Trophy, Users, BarChart3, Download, FileSpreadsheet, FileText } from 'lucide-react';
import jsPDF from 'jspdf';
import 'jspdf-autotable';
import * as XLSX from 'xlsx';
import { PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis, CartesianGrid, ResponsiveContainer, Tooltip, Legend } from 'recharts';
import { voteService } from '../services/voteService';
import { scoreService } from '../services/scoreService';
import { CONTENT_TYPES, POLL_TYPE_KEY, QUESTION_TYPE_KEY } from '../config/constants';
import { useTranslation } from 'react-i18next';

const CHART_COLORS = ['#6366F1', '#EC4899', '#10B981', '#F59E0B', '#8B5CF6', '#3B82F6', '#EF4444', '#14B8A6'];

export default function ResultsAnalysis({ poll, pollId, onClose }) {
  const { t } = useTranslation();
  const [aggregates, setAggregates] = useState([]);
  const [openAnswers, setOpenAnswers] = useState([]);
  const [scores, setScores] = useState([]);
  const [activeTab, setActiveTab] = useState('overview');
  const [chartType, setChartType] = useState('bar');

  useEffect(() => {
    if (!pollId) return;
    voteService.aggregates(pollId).then(setAggregates).catch(() => {});
    voteService.listOpen(pollId).then(setOpenAnswers).catch(() => {});
    scoreService.leaderboard(pollId).then(setScores).catch(() => {});
  }, [pollId]);

  const typeKey = poll ? POLL_TYPE_KEY[poll.type] : null;
  const typeConfig = typeKey ? CONTENT_TYPES[typeKey] : CONTENT_TYPES.contest;

  const analytics = useMemo(() => {
    if (!poll || aggregates.length === 0) return null;
    const totalParticipants = Math.max(...aggregates.map((a) => a.totalRespondents || 0), 0);
    const questionAnalysis = poll.questions.map((q, i) => {
      const agg = aggregates.find((a) => a.questionIndex === i);
      const total = agg?.totalRespondents || 0;
      const opts = (q.options || []).map((opt, oi) => ({
        text: opt.text,
        votes: agg?.optionCounts?.[oi] || 0,
        percentage: total > 0 ? Math.round(((agg?.optionCounts?.[oi] || 0) / total) * 100) : 0,
        isCorrect: q.correctOptionIndex === oi,
      }));
      return { question: q.text, total, options: opts };
    });
    return { totalParticipants, questionAnalysis };
  }, [poll, aggregates]);

  const handleExportPdf = () => {
    const doc = new jsPDF();
    doc.text(poll.title, 14, 16);
    doc.autoTable({
      head: [['#', t('results.pdfUser'), t('results.pdfScore'), t('results.pdfTime')]],
      body: scores.map((s, i) => [i + 1, s.userName, s.points, s.totalTimeMs]),
      startY: 24,
    });
    doc.save(`${poll.title || 'report'}.pdf`);
  };

  const handleExportExcel = () => {
    const data = [
      [t('results.pdfUser'), t('results.pdfScore'), t('results.pdfTime')],
      ...scores.map((s) => [s.userName, s.points, s.totalTimeMs]),
    ];
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(data), t('results.sheetScores'));
    XLSX.writeFile(wb, `${poll.title || 'report'}.xlsx`);
  };

  if (!poll) return null;

  return (
    <div className="fixed inset-0 z-50 bg-slate-50 overflow-y-auto">
      <header className="bg-slate-900 text-white p-4 flex items-center justify-between sticky top-0 z-10">
        <h2 className="font-bold text-lg">📊 {t('results.title', { title: poll.title })}</h2>
        <div className="flex gap-2">
          <button onClick={handleExportExcel} className="px-3 py-2 bg-emerald-600 hover:bg-emerald-700 rounded-lg text-sm flex items-center gap-1"><FileSpreadsheet size={16} /> Excel</button>
          <button onClick={handleExportPdf} className="px-3 py-2 bg-rose-600 hover:bg-rose-700 rounded-lg text-sm flex items-center gap-1"><FileText size={16} /> PDF</button>
          <button onClick={onClose} className="px-3 py-2 bg-white/10 hover:bg-white/20 rounded-lg text-sm">{t('common.close')}</button>
        </div>
      </header>

      <div className="p-6 max-w-5xl mx-auto">
        <div className="flex gap-2 mb-4">
          <button onClick={() => setActiveTab('overview')} className={`px-4 py-2 rounded-lg text-sm ${activeTab === 'overview' ? 'bg-indigo-600 text-white' : 'bg-white border border-slate-200'}`}><BarChart3 size={16} className="inline mr-1" /> {t('results.questionsTab')}</button>
          {typeConfig.hasCorrectAnswer && (
            <button onClick={() => setActiveTab('leaderboard')} className={`px-4 py-2 rounded-lg text-sm ${activeTab === 'leaderboard' ? 'bg-indigo-600 text-white' : 'bg-white border border-slate-200'}`}><Trophy size={16} className="inline mr-1" /> {t('results.leaderboardTab')}</button>
          )}
        </div>

        {analytics && (
          <div className="bg-white rounded-xl p-4 border border-slate-200 mb-4 flex items-center gap-3">
            <Users className="text-indigo-600" />
            <div>
              <div className="text-2xl font-bold">{analytics.totalParticipants}</div>
              <div className="text-xs text-slate-500">{t('results.totalParticipants')}</div>
            </div>
          </div>
        )}

        {activeTab === 'overview' && analytics && (
          <div className="space-y-4">
            {analytics.questionAnalysis.map((qa, i) => (
              <div key={i} className="bg-white rounded-xl p-4 border border-slate-200">
                <h3 className="font-bold mb-3">{t('results.questionLabel', { n: i + 1, text: qa.question })}</h3>
                {qa.options.length > 0 ? (
                  <ResponsiveContainer width="100%" height={Math.max(qa.options.length * 40, 160)}>
                    {chartType === 'pie' ? (
                      <PieChart>
                        <Pie data={qa.options} dataKey="votes" nameKey="text" label>
                          {qa.options.map((_, idx) => <Cell key={idx} fill={CHART_COLORS[idx % CHART_COLORS.length]} />)}
                        </Pie>
                        <Tooltip />
                        <Legend />
                      </PieChart>
                    ) : (
                      <BarChart data={qa.options} layout="vertical">
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis type="number" />
                        <YAxis type="category" dataKey="text" width={140} />
                        <Tooltip />
                        <Bar dataKey="votes">
                          {qa.options.map((o, idx) => (
                            <Cell key={idx} fill={o.isCorrect ? '#10B981' : CHART_COLORS[idx % CHART_COLORS.length]} />
                          ))}
                        </Bar>
                      </BarChart>
                    )}
                  </ResponsiveContainer>
                ) : (
                  <p className="text-slate-400 italic text-sm">{t('results.notMultipleChoice')}</p>
                )}
              </div>
            ))}
          </div>
        )}

        {activeTab === 'leaderboard' && (
          <div className="bg-white rounded-xl border border-slate-200 divide-y">
            {scores.map((s, i) => (
              <div key={s.userName} className="p-3 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <span className="font-bold text-slate-400 w-8">{i + 1}</span>
                  <span className="font-medium">{s.userName}</span>
                </div>
                <span className="font-bold text-indigo-600">{t('results.points', { n: s.points })}</span>
              </div>
            ))}
            {scores.length === 0 && <p className="p-4 text-slate-400 text-center">{t('results.noScores')}</p>}
          </div>
        )}

        {openAnswers.length > 0 && (
          <div className="mt-6 bg-white rounded-xl p-4 border border-slate-200">
            <h3 className="font-bold mb-3">{t('results.openAnswers', { n: openAnswers.length })}</h3>
            <ul className="space-y-2">
              {openAnswers.map((a) => (
                <li key={a.id} className="bg-slate-50 rounded-lg p-2 text-sm">
                  <span className="text-slate-400 text-xs">{a.userName}: </span>
                  {a.answerText}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}
