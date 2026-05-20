import React from 'react';
import { User, LogOut, Shield, ChevronRight, Globe, Info, Bell, Moon, HelpCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const ProfileScreen = ({ user, isAdmin, isAuthorized, onLogout, onNavigateToAdmin }) => {
    const { t, i18n } = useTranslation();
    const lang = i18n.resolvedLanguage || i18n.language;
    const setLang = (l) => i18n.changeLanguage(l);
    return (
        <div className="h-full w-full bg-slate-50 flex flex-col overflow-hidden">
            {/* Header */}
            <div className="bg-gradient-to-br from-indigo-600 to-purple-600 pt-12 pb-8 px-6">
                <div className="flex items-center gap-4">
                    <div className="w-16 h-16 bg-white/20 rounded-full flex items-center justify-center">
                        {user.photoURL ? (
                            <img src={user.photoURL} alt="Profile" className="w-full h-full rounded-full object-cover" />
                        ) : (
                            <User size={32} className="text-white" />
                        )}
                    </div>
                    <div className="flex-1">
                        <h2 className="text-xl font-bold text-white">{user.displayName || t('common.user')}</h2>
                        <p className="text-white/70 text-sm truncate">{user.email}</p>
                        {isAdmin && (
                            <span className="inline-flex items-center gap-1 bg-amber-500/30 text-amber-200 px-2 py-0.5 rounded text-xs mt-1">
                                <Shield size={10} /> Admin
                            </span>
                        )}
                    </div>
                </div>
            </div>

            {/* Menu Items */}
            <div className="flex-1 overflow-y-auto p-4 space-y-2">
                {isAdmin && (
                    <button
                        onClick={onNavigateToAdmin}
                        className="w-full flex items-center gap-4 bg-white p-4 rounded-2xl shadow-sm border border-slate-100"
                    >
                        <div className="w-10 h-10 bg-amber-100 rounded-xl flex items-center justify-center">
                            <Shield size={20} className="text-amber-600" />
                        </div>
                        <div className="flex-1 text-left">
                            <div className="font-semibold text-slate-800">{t('profile.admin')}</div>
                            <div className="text-xs text-slate-500">{t('profile.adminDesc')}</div>
                        </div>
                        <ChevronRight size={20} className="text-slate-400" />
                    </button>
                )}

                {/* Language Selector */}
                <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-4">
                    <div className="flex items-center gap-4">
                        <div className="w-10 h-10 bg-sky-100 rounded-xl flex items-center justify-center">
                            <Globe size={20} className="text-sky-600" />
                        </div>
                        <div className="flex-1">
                            <div className="font-semibold text-slate-800">{t('profile.language')}</div>
                        </div>
                        <div className="flex gap-1">
                            <button
                                onClick={() => setLang('tr')}
                                className={`px-3 py-1.5 rounded-lg text-xs font-bold ${lang === 'tr' ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}
                            >
                                TR
                            </button>
                            <button
                                onClick={() => setLang('en')}
                                className={`px-3 py-1.5 rounded-lg text-xs font-bold ${lang === 'en' ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}
                            >
                                EN
                            </button>
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-2xl shadow-sm border border-slate-100 divide-y divide-slate-100">
                    <button className="w-full flex items-center gap-4 p-4">
                        <div className="w-10 h-10 bg-indigo-100 rounded-xl flex items-center justify-center">
                            <Bell size={20} className="text-indigo-600" />
                        </div>
                        <div className="flex-1 text-left">
                            <div className="font-semibold text-slate-800">{t('profile.notifications')}</div>
                            <div className="text-xs text-slate-500">{t('profile.notificationsDesc')}</div>
                        </div>
                        <ChevronRight size={20} className="text-slate-400" />
                    </button>

                    <button className="w-full flex items-center gap-4 p-4">
                        <div className="w-10 h-10 bg-slate-100 rounded-xl flex items-center justify-center">
                            <Moon size={20} className="text-slate-600" />
                        </div>
                        <div className="flex-1 text-left">
                            <div className="font-semibold text-slate-800">{t('profile.appearance')}</div>
                            <div className="text-xs text-slate-500">{t('profile.appearanceDesc')}</div>
                        </div>
                        <ChevronRight size={20} className="text-slate-400" />
                    </button>
                </div>

                <div className="bg-white rounded-2xl shadow-sm border border-slate-100 divide-y divide-slate-100">
                    <button className="w-full flex items-center gap-4 p-4">
                        <div className="w-10 h-10 bg-emerald-100 rounded-xl flex items-center justify-center">
                            <HelpCircle size={20} className="text-emerald-600" />
                        </div>
                        <div className="flex-1 text-left">
                            <div className="font-semibold text-slate-800">{t('profile.help')}</div>
                            <div className="text-xs text-slate-500">{t('profile.helpDesc')}</div>
                        </div>
                        <ChevronRight size={20} className="text-slate-400" />
                    </button>

                    <button className="w-full flex items-center gap-4 p-4">
                        <div className="w-10 h-10 bg-blue-100 rounded-xl flex items-center justify-center">
                            <Info size={20} className="text-blue-600" />
                        </div>
                        <div className="flex-1 text-left">
                            <div className="font-semibold text-slate-800">{t('profile.about')}</div>
                            <div className="text-xs text-slate-500">{t('profile.version')}</div>
                        </div>
                        <ChevronRight size={20} className="text-slate-400" />
                    </button>
                </div>

                {/* Logout Button */}
                <button
                    onClick={onLogout}
                    className="w-full flex items-center gap-4 bg-red-50 p-4 rounded-2xl border border-red-100"
                >
                    <div className="w-10 h-10 bg-red-100 rounded-xl flex items-center justify-center">
                        <LogOut size={20} className="text-red-600" />
                    </div>
                    <div className="flex-1 text-left">
                        <div className="font-semibold text-red-600">{t('profile.logout')}</div>
                        <div className="text-xs text-red-400">{t('profile.logoutDesc')}</div>
                    </div>
                </button>
            </div>
        </div>
    );
};

export default ProfileScreen;
