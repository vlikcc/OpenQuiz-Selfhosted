export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';
export const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID || '';

export const COLORS = ['#4F46E5', '#EC4899', '#10B981', '#F59E0B', '#8B5CF6', '#3B82F6'];

// Maps to backend OpenQuiz.Domain.Enums.PollType (numeric).
export const POLL_TYPE_VALUE = {
  contest: 1,
  survey: 2,
  quiz: 3,
  exam: 4,
  wordcloud: 5,
};

export const POLL_TYPE_KEY = Object.fromEntries(
  Object.entries(POLL_TYPE_VALUE).map(([k, v]) => [v, k]),
);

// Maps to backend OpenQuiz.Domain.Enums.QuestionType.
export const QUESTION_TYPE_VALUE = {
  multiple: 1,
  open: 2,
  wordcloud: 3,
};

export const QUESTION_TYPE_KEY = Object.fromEntries(
  Object.entries(QUESTION_TYPE_VALUE).map(([k, v]) => [v, k]),
);

export const POLL_STATUS_VALUE = { waiting: 1, live: 2, ended: 3 };
export const POLL_STATUS_KEY = Object.fromEntries(
  Object.entries(POLL_STATUS_VALUE).map(([k, v]) => [v, k]),
);

export const CONTENT_TYPES = {
  contest: {
    label: 'Yarışma',
    icon: '🏆',
    color: 'indigo',
    description: 'Doğru cevaplı sorular, puanlama sistemi',
    hasCorrectAnswer: true,
    multipleQuestions: true,
    questionType: 'multiple',
  },
  survey: {
    label: 'Anket',
    icon: '📊',
    color: 'emerald',
    description: 'Fikir toplama, doğru cevap yok',
    hasCorrectAnswer: false,
    multipleQuestions: true,
    questionType: 'multiple',
  },
  quiz: {
    label: 'Quiz',
    icon: '❓',
    color: 'amber',
    description: 'Tek sorulu hızlı test',
    hasCorrectAnswer: true,
    multipleQuestions: true,
    questionType: 'multiple',
  },
  exam: {
    label: 'Sınav',
    icon: '📝',
    color: 'rose',
    description: 'Çoktan seçmeli ve açık uçlu sorular, KaTeX formül desteği',
    hasCorrectAnswer: true,
    multipleQuestions: true,
    questionType: 'mixed',
    supportsKatex: true,
  },
  wordcloud: {
    label: 'Kelime Bulutu',
    icon: '☁️',
    color: 'sky',
    description: 'Katılımcıların yazdığı kelimelerden anlık bulut',
    hasCorrectAnswer: false,
    multipleQuestions: true,
    questionType: 'wordcloud',
  },
};
