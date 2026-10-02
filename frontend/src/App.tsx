import { Navigate, Route, Routes } from 'react-router-dom';
import Layout from './components/Layout';
import AboutPage from './pages/AboutPage';
import AdminDashboardPage from './pages/AdminDashboardPage';
import HomePage from './pages/HomePage';
import LoginPage from './pages/LoginPage';
import OverviewPage from './pages/OverviewPage';
import PropertiesPage from './pages/PropertiesPage';
import ActivitiesPage from './pages/ActivitiesPage';
import AlertsPage from './pages/AlertsPage';
import ReportsPage from './pages/ReportsPage';
import RegisterPage from './pages/RegisterPage';
import UserDashboardPage from './pages/UserDashboardPage';
import ProfilePage from './pages/ProfilePage';
import SettingsPage from './pages/SettingsPage';

type AppRole = 'Administrator' | 'Resident User';

function getStoredRole(): string | null {
  try {
    return (JSON.parse(localStorage.getItem('smartguard-user') ?? 'null') as { role?: string } | null)?.role ?? null;
  } catch {
    return null;
  }
}

function RequireAuth({ children, roles }: { children: JSX.Element; roles?: AppRole[] }) {
  const token = localStorage.getItem('smartguard-token');
  const role = getStoredRole();

  if (!token || (role !== 'Administrator' && role !== 'Resident User')) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(role as AppRole)) {
    return <Navigate to={role === 'Administrator' ? '/admin-dashboard' : '/user-dashboard'} replace />;
  }

  return children;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/about" element={<AboutPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route element={<RequireAuth roles={['Resident User']}><Layout /></RequireAuth>}>
        <Route path="/user-dashboard" element={<UserDashboardPage />} />
      </Route>
      <Route element={<RequireAuth roles={['Administrator']}><Layout /></RequireAuth>}>
        <Route path="/admin-dashboard" element={<AdminDashboardPage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Route>
      <Route element={<RequireAuth><Layout /></RequireAuth>}>
        <Route path="/properties" element={<PropertiesPage />} />
        <Route path="/activities" element={<ActivitiesPage />} />
        <Route path="/alerts" element={<AlertsPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/overview" element={<OverviewPage />} />
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
