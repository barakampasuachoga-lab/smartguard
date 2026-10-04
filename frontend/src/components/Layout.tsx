import { Activity, Bell, Building2, ChartNoAxesCombined, CreditCard, House, LogOut, ReceiptText, Settings2, ShieldCheck, UserRound, Users } from 'lucide-react';
import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { clearStoredSession, getProfilePhotoUrl } from '../api';

const commonItems = [
  { label: 'Properties', to: '/properties', icon: Building2 },
  { label: 'Events', to: '/activities', icon: Activity },
  { label: 'Alerts', to: '/alerts', icon: Bell },
  { label: 'Intelligence', to: '/overview', icon: ShieldCheck },
  { label: 'Reports', to: '/reports', icon: ChartNoAxesCombined },
  { label: 'Profile', to: '/profile', icon: UserRound },
];

export default function Layout() {
  const navigate = useNavigate();
  const location = useLocation();
  const [user, setUser] = useState(() => JSON.parse(localStorage.getItem('smartguard-user') ?? '{}') as { fullName?: string; name?: string; profilePhotoUrl?: string | null; role?: string });

  useEffect(() => {
    const refreshUser = () => setUser(JSON.parse(localStorage.getItem('smartguard-user') ?? '{}'));
    window.addEventListener('smartguard-profile-updated', refreshUser);
    return () => window.removeEventListener('smartguard-profile-updated', refreshUser);
  }, []);

  const fullName = user.fullName ?? user.name;
  const isAdmin = user.role === 'Administrator';
  const dashboardPath = isAdmin ? '/admin-dashboard' : '/user-dashboard';
  const pageTitles: Record<string, string> = {
    '/admin-dashboard': 'Administration',
    '/user-dashboard': 'Security overview',
    '/properties': 'Properties',
    '/activities': 'Security events',
    '/alerts': 'Alerts',
    '/reports': 'Reports',
    '/overview': 'Security intelligence',
    '/profile': 'Profile settings',
    '/subscription': 'Subscription',
    '/billing': 'Billing history',
    '/admin/billing': 'Billing & subscriptions',
  };

  const signOut = () => {
    clearStoredSession();
    navigate('/', { replace: true });
  };

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <a className="app-brand" href="/" aria-label="SmartGuard home">
          <span className="brand-mark"><ShieldCheck size={20} /></span>
          <span><strong>SMARTGUARD</strong><small>Property security</small></span>
        </a>
        <div className="sidebar-label">Workspace</div>
        <nav className="workspace-nav" aria-label="Workspace navigation">
          <NavLink to={dashboardPath} className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}>
            <House size={17} /><span>Dashboard</span>
          </NavLink>
          {isAdmin && <NavLink to="/admin-dashboard" className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}>
            <Users size={17} /><span>User access</span>
          </NavLink>}
          {isAdmin && <NavLink to="/settings" className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}>
            <Settings2 size={17} /><span>System settings</span>
          </NavLink>}
          {isAdmin && <NavLink to="/admin/billing" className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}>
            <CreditCard size={17} /><span>Billing admin</span>
          </NavLink>}
          {commonItems.map(({ label, to, icon: Icon }) => (
            <NavLink key={to} to={to} className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}>
              <Icon size={17} /><span>{label}</span>
            </NavLink>
          ))}
          <div className="sidebar-label" style={{ marginTop: 14 }}>Account</div>
          <NavLink to="/subscription" className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}>
            <CreditCard size={17} /><span>Subscription</span>
          </NavLink>
          <NavLink to="/billing" className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}>
            <ReceiptText size={17} /><span>Billing</span>
          </NavLink>
        </nav>
        <div className="sidebar-account">
          <div className="account-avatar">{user.profilePhotoUrl ? <img src={getProfilePhotoUrl(user.profilePhotoUrl) ?? undefined} alt="" /> : fullName?.trim().charAt(0) || 'S'}</div>
          <div className="account-copy"><strong>{fullName ?? 'SmartGuard User'}</strong><span>{user.role ?? 'Signed in'}</span></div>
          <button className="icon-button signout-button" onClick={signOut} title="Sign out" aria-label="Sign out">
            <LogOut size={17} />
          </button>
        </div>
      </aside>
      <main className="app-main">
        <header className="app-topbar">
          <div>
            <p className="eyebrow">SmartGuard / Workspace</p>
            <h1>{pageTitles[location.pathname] ?? 'Security workspace'}</h1>
          </div>
          <div className="topbar-actions">
            <span className={`role-tag ${isAdmin ? 'role-admin' : ''}`}>{user.role ?? 'User'}</span>
            <button className="topbar-signout" onClick={signOut} aria-label="Sign out"><LogOut size={15} /><span>Sign out</span></button>
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}
