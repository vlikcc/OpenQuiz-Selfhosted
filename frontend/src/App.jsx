import React, { useState, useEffect, useCallback } from 'react';
import { Trophy, Loader2, CheckCircle2, AlertTriangle } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { CONTENT_TYPES, POLL_TYPE_KEY } from './config/constants';
import { useAuth } from './hooks/useAuth';
import { authService } from './services/authService';
import { pollService } from './services/pollService';
import { tokenStore } from './services/tokenStore';

import VoterMode from './components/VoterMode';
import Dashboard from './components/Dashboard';
import PresenterMode from './components/PresenterMode';
import AdminPanel from './components/AdminPanel';
import TabBar from './components/TabBar';
import AuthScreen from './components/AuthScreen';
import ProfileScreen from './components/ProfileScreen';
import LandingPage from './components/LandingPage';

const urlParams = new URLSearchParams(window.location.search);
const isVoterMode = urlParams.get('mode') === 'voter' && !!urlParams.get('id');
const initialPollId = urlParams.get('id');

const VoterLoadingScreen = ({ pollData }) => {
  const { t } = useTranslation();
  return (
    <div className="h-full w-full flex flex-col items-center justify-center bg-gradient-to-br from-indigo-600 to-purple-700 text-white p-6">
      <div className="text-center">
        {pollData ? (
          <>
            <div className="text-6xl mb-4">{CONTENT_TYPES[POLL_TYPE_KEY[pollData.type]]?.icon || '🎯'}</div>
            <h1 className="text-2xl font-bold mb-2">{pollData.title}</h1>
            <p className="text-indigo-200 mb-6">{pollData.questions?.length || 0} {t('common.question')}</p>
          </>
        ) : (
          <div className="w-16 h-16 bg-white/20 rounded-2xl flex items-center justify-center mx-auto mb-4">
            <Trophy size={32} />
          </div>
        )}
        <div className="flex items-center justify-center gap-3">
          <Loader2 className="animate-spin" size={24} />
          <span className="text-lg font-medium">{t('common.preparing')}</span>
        </div>
      </div>
    </div>
  );
};

const LoadingScreen = () => {
  const { t } = useTranslation();
  return (
    <div className="h-full w-full flex items-center justify-center bg-slate-50">
      <div className="text-center">
        <Loader2 className="animate-spin text-indigo-600 mx-auto mb-4" size={40} />
        <p className="text-slate-500">{t('common.loading')}</p>
      </div>
    </div>
  );
};

class ErrorBoundary extends React.Component {
  constructor(props) { super(props); this.state = { hasError: false }; }
  static getDerivedStateFromError() { return { hasError: true }; }
  componentDidCatch(error, info) { console.error('Error:', error, info); }
  render() {
    if (this.state.hasError) return <div className="p-10 text-center">{this.props.fallbackMessage}</div>;
    return this.props.children;
  }
}

export default function App() {
  const { t } = useTranslation();
  const { user, loading: authLoading, logout } = useAuth();
  const isAdmin = !!user?.isAdmin;
  const isAuthorized = !!user?.canCreate || isAdmin;

  const [preloadedPoll, setPreloadedPoll] = useState(null);
  const [activeTab, setActiveTab] = useState('dashboard');
  const [activeScreen, setActiveScreen] = useState('tabs');
  const [activePollId, setActivePollId] = useState(() => initialPollId || localStorage.getItem('activePollId'));
  const [toast, setToast] = useState(null);
  const [isCreating, setIsCreating] = useState(false);
  const [showLandingPage, setShowLandingPage] = useState(true);

  // QR voter preload
  useEffect(() => {
    if (!isVoterMode || !initialPollId) return;
    pollService.get(initialPollId).then(setPreloadedPoll).catch(console.error);
  }, []);

  useEffect(() => {
    if (activePollId) localStorage.setItem('activePollId', activePollId);
  }, [activePollId]);

  const showToast = useCallback((message, type = 'success') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3000);
  }, []);

  const navigate = useCallback((screen, id = null) => {
    if (id) setActivePollId(id);
    if (screen === 'dashboard') { setActiveScreen('tabs'); setActiveTab('dashboard'); }
    else setActiveScreen(screen);
  }, []);

  const handleTabChange = (tab) => {
    if (tab === 'create') { setIsCreating(true); setActiveTab('dashboard'); }
    else { setIsCreating(false); setActiveTab(tab); }
  };

  const handleGoogleLogin = async (idToken) => {
    try {
      await authService.googleLogin(idToken);
      showToast(t('auth.loginSuccess'));
    } catch (err) {
      console.error(err);
      showToast(err.message || t('auth.googleFailed'), 'error');
      throw err;
    }
  };

  const handleEmailLogin = async (email, password) => {
    await authService.login(email, password);
    showToast(t('auth.loginSuccess'));
  };

  const handleEmailRegister = async (email, password, displayName) => {
    await authService.register(email, password, displayName);
    showToast(t('auth.registerSuccess'));
  };

  const handleLogout = async () => {
    await logout();
    setActiveTab('dashboard');
    setActiveScreen('tabs');
    showToast(t('auth.logoutSuccess'));
  };

  const handlePasswordReset = async (email) => {
    await authService.passwordResetRequest(email);
  };

  // QR Voter Mode
  if (isVoterMode && initialPollId) {
    if (authLoading && tokenStore.getAccessToken()) return <VoterLoadingScreen pollData={preloadedPoll} />;
    return (
      <ErrorBoundary fallbackMessage={t('common.errorRefresh')}>
        <div className="h-full w-full flex flex-col overflow-hidden">
          <VoterMode
            pollId={activePollId}
            onExit={() => {}}
            user={user}
            showToast={showToast}
            preloadedPoll={preloadedPoll}
          />
        </div>
        {toast && (
          <div className={`fixed bottom-20 left-4 right-4 px-6 py-3 rounded-lg shadow-lg text-white font-medium flex items-center gap-2 animate-bounce-in z-50 ${toast.type === 'error' ? 'bg-red-600' : 'bg-slate-800'}`}>
            {toast.type === 'success' ? <CheckCircle2 size={18} /> : <AlertTriangle size={18} />}
            {toast.message}
          </div>
        )}
      </ErrorBoundary>
    );
  }

  if (authLoading) return <LoadingScreen />;

  if (!user) {
    if (!showLandingPage) {
      return (
        <ErrorBoundary fallbackMessage={t('common.errorRefresh')}>
          <AuthScreen
            onGoogleLogin={handleGoogleLogin}
            onEmailLogin={handleEmailLogin}
            onEmailRegister={handleEmailRegister}
            onPasswordReset={handlePasswordReset}
            isLoading={false}
            onBack={() => setShowLandingPage(true)}
          />
        </ErrorBoundary>
      );
    }
    return (
      <ErrorBoundary fallbackMessage={t('common.errorRefresh')}>
        <LandingPage onLogin={() => setShowLandingPage(false)} />
      </ErrorBoundary>
    );
  }

  if (activeScreen === 'presenter' && activePollId) {
    return (
      <ErrorBoundary fallbackMessage={t('common.errorRefresh')}>
        <div className="h-full w-full flex flex-col overflow-hidden">
          <PresenterMode
            pollId={activePollId}
            onExit={() => navigate('dashboard')}
            onSwitchToVoter={() => navigate('voter')}
            showToast={showToast}
          />
        </div>
        {toast && (
          <div className={`fixed bottom-6 right-6 px-6 py-3 rounded-lg shadow-lg text-white font-medium flex items-center gap-2 animate-bounce-in z-50 ${toast.type === 'error' ? 'bg-red-600' : 'bg-slate-800'}`}>
            {toast.type === 'success' ? <CheckCircle2 size={18} /> : <AlertTriangle size={18} />}
            {toast.message}
          </div>
        )}
      </ErrorBoundary>
    );
  }

  if (activeScreen === 'voter' && activePollId) {
    return (
      <ErrorBoundary fallbackMessage={t('common.errorRefresh')}>
        <div className="h-full w-full flex flex-col overflow-hidden">
          <VoterMode pollId={activePollId} onExit={() => navigate('dashboard')} user={user} showToast={showToast} />
        </div>
      </ErrorBoundary>
    );
  }

  if (activeScreen === 'admin' && isAdmin) {
    return (
      <ErrorBoundary fallbackMessage={t('common.errorRefresh')}>
        <div className="h-full w-full flex flex-col overflow-hidden bg-slate-50">
          <div className="bg-slate-900 text-white p-4 flex items-center gap-4">
            <button onClick={() => navigate('dashboard')} className="text-white/70 hover:text-white">← {t('common.back')}</button>
            <h1 className="font-bold">{t('admin.title')}</h1>
          </div>
          <div className="flex-1 overflow-y-auto">
            <AdminPanel showToast={showToast} />
          </div>
        </div>
      </ErrorBoundary>
    );
  }

  return (
    <ErrorBoundary fallbackMessage={t('common.errorRefresh')}>
      <div className="h-full w-full bg-slate-50 flex flex-col overflow-hidden">
        {toast && (
          <div className={`fixed top-4 left-4 right-4 px-6 py-3 rounded-lg shadow-lg text-white font-medium flex items-center gap-2 animate-bounce-in z-50 ${toast.type === 'error' ? 'bg-red-600' : 'bg-slate-800'}`}>
            {toast.type === 'success' ? <CheckCircle2 size={18} /> : <AlertTriangle size={18} />}
            {toast.message}
          </div>
        )}

        <div className="flex-1 overflow-hidden pb-16">
          {activeTab === 'dashboard' && (
            <Dashboard
              onNavigate={navigate}
              user={user}
              showToast={showToast}
              isAdmin={isAdmin}
              isAuthorized={isAuthorized}
              isCreating={isCreating}
              setIsCreating={setIsCreating}
            />
          )}
          {activeTab === 'profile' && (
            <ProfileScreen
              user={user}
              isAdmin={isAdmin}
              isAuthorized={isAuthorized}
              onLogout={handleLogout}
              onNavigateToAdmin={() => setActiveScreen('admin')}
            />
          )}
        </div>

        <TabBar activeTab={activeTab} onTabChange={handleTabChange} isAdmin={isAdmin} />
      </div>
    </ErrorBoundary>
  );
}
