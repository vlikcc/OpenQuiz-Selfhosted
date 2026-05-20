import { useState, useEffect } from 'react';
import { Shield, UserPlus, Users, Mail, Plus, Trash2, Loader2, UserCircle, Calendar, AlertTriangle } from 'lucide-react';
import { userService } from '../services/userService';

export default function AdminPanel({ showToast }) {
  const [newEmail, setNewEmail] = useState('');
  const [isAdding, setIsAdding] = useState(false);
  const [authorizedUsers, setAuthorizedUsers] = useState([]);
  const [registeredUsers, setRegisteredUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeSection, setActiveSection] = useState('authorized');

  const refresh = async () => {
    setLoading(true);
    try {
      const [auth, reg] = await Promise.all([userService.listAuthorized(), userService.listRegistered()]);
      setAuthorizedUsers(auth);
      setRegisteredUsers(reg);
    } catch (e) {
      showToast(e.message || 'Yüklenemedi', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { refresh(); }, []);

  const handleAddUser = async (e) => {
    e.preventDefault();
    const email = newEmail.trim().toLowerCase();
    if (!email || !email.includes('@')) return showToast('Geçerli bir email girin', 'error');

    setIsAdding(true);
    try {
      await userService.addAuthorized(email);
      setNewEmail('');
      showToast('Kullanıcı yetkilendirildi!');
      await refresh();
    } catch (err) {
      showToast(err.message || 'Hata oluştu', 'error');
    } finally {
      setIsAdding(false);
    }
  };

  const handleRemove = async (email) => {
    if (!confirm(`${email} silinecek. Emin misiniz?`)) return;
    try {
      await userService.removeAuthorized(email);
      showToast('Yetki kaldırıldı');
      await refresh();
    } catch (err) {
      showToast(err.message || 'Hata oluştu', 'error');
    }
  };

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center gap-2 mb-6">
        <Shield className="text-indigo-600" />
        <h2 className="text-xl font-bold">Admin Paneli</h2>
      </div>

      <div className="flex gap-2 mb-6">
        <button
          onClick={() => setActiveSection('authorized')}
          className={`px-4 py-2 rounded-lg font-medium text-sm flex items-center gap-2 ${activeSection === 'authorized' ? 'bg-indigo-600 text-white' : 'bg-white border border-slate-200 text-slate-600 hover:border-indigo-400'}`}
        >
          <UserPlus size={16} /> Yetkili Kullanıcılar ({authorizedUsers.length})
        </button>
        <button
          onClick={() => setActiveSection('registered')}
          className={`px-4 py-2 rounded-lg font-medium text-sm flex items-center gap-2 ${activeSection === 'registered' ? 'bg-indigo-600 text-white' : 'bg-white border border-slate-200 text-slate-600 hover:border-indigo-400'}`}
        >
          <Users size={16} /> Kayıtlı Kullanıcılar ({registeredUsers.length})
        </button>
      </div>

      {loading && <Loader2 className="animate-spin text-indigo-600" />}

      {!loading && activeSection === 'authorized' && (
        <div className="space-y-4">
          <form onSubmit={handleAddUser} className="bg-white border border-slate-200 rounded-xl p-4 flex gap-2">
            <div className="relative flex-1">
              <Mail size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                value={newEmail}
                onChange={(e) => setNewEmail(e.target.value)}
                placeholder="kullanici@ornek.com"
                type="email"
                className="w-full pl-9 pr-3 py-2 border border-slate-200 rounded-lg outline-none focus:border-indigo-500"
              />
            </div>
            <button type="submit" disabled={isAdding} className="bg-indigo-600 text-white px-4 py-2 rounded-lg flex items-center gap-1 disabled:opacity-50">
              {isAdding ? <Loader2 size={16} className="animate-spin" /> : <Plus size={16} />}
              Ekle
            </button>
          </form>

          <ul className="space-y-2">
            {authorizedUsers.map((u) => (
              <li key={u.email} className="bg-white border border-slate-200 rounded-xl p-3 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <UserCircle className="text-slate-400" />
                  <div>
                    <div className="font-medium text-slate-800">{u.email}</div>
                    <div className="text-xs text-slate-400 flex items-center gap-1">
                      <Calendar size={12} /> {new Date(u.addedAt).toLocaleDateString('tr-TR')}
                    </div>
                  </div>
                </div>
                <button onClick={() => handleRemove(u.email)} className="text-red-400 hover:text-red-600 p-2">
                  <Trash2 size={16} />
                </button>
              </li>
            ))}
            {authorizedUsers.length === 0 && (
              <li className="text-center text-slate-400 py-6 flex flex-col items-center gap-2">
                <AlertTriangle size={20} />
                Henüz yetkili kullanıcı yok.
              </li>
            )}
          </ul>
        </div>
      )}

      {!loading && activeSection === 'registered' && (
        <ul className="space-y-2">
          {registeredUsers.map((u) => (
            <li key={u.id} className="bg-white border border-slate-200 rounded-xl p-3 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <UserCircle className="text-slate-400" />
                <div>
                  <div className="font-medium text-slate-800">{u.displayName || u.email}</div>
                  <div className="text-xs text-slate-500">{u.email}</div>
                  <div className="text-xs text-slate-400 flex items-center gap-1">
                    <Calendar size={12} /> {new Date(u.createdAt).toLocaleDateString('tr-TR')}
                  </div>
                </div>
              </div>
              {u.canCreate && (
                <span className="text-xs font-bold uppercase px-2 py-1 rounded bg-emerald-100 text-emerald-700">Yetkili</span>
              )}
            </li>
          ))}
          {registeredUsers.length === 0 && (
            <li className="text-center text-slate-400 py-6">Henüz kayıtlı kullanıcı yok.</li>
          )}
        </ul>
      )}
    </div>
  );
}
