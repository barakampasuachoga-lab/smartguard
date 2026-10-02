import { LockKeyhole, Mail, ShieldCheck } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { loginUser } from '../api';

export default function LoginPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState(() => localStorage.getItem('smartguard-remembered-email') ?? '');
  const [password, setPassword] = useState('');
  const [rememberEmail, setRememberEmail] = useState(() => Boolean(localStorage.getItem('smartguard-remembered-email')));
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (localStorage.getItem('smartguard-token')) {
      const storedUser = JSON.parse(localStorage.getItem('smartguard-user') ?? '{}') as { role?: string };
      if (storedUser.role === 'Administrator') {
        navigate('/admin-dashboard', { replace: true });
      } else {
        navigate('/user-dashboard', { replace: true });
      }
    }
  }, [navigate]);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!email || !password) {
      setError('Please enter both email and password.');
      return;
    }

    if (!email.includes('@')) {
      setError('Please use a valid email address.');
      return;
    }

    try {
      setIsSubmitting(true);
      setError('');
      const response = await loginUser({ email, password });
      const sessionUser = response.user;

      if (rememberEmail) {
        localStorage.setItem('smartguard-remembered-email', sessionUser.email);
      } else {
        localStorage.removeItem('smartguard-remembered-email');
      }

      localStorage.setItem('smartguard-token', response.token);
      localStorage.setItem('smartguard-user', JSON.stringify({
        id: sessionUser.id,
        fullName: sessionUser.fullName,
        email: sessionUser.email,
        profilePhotoUrl: sessionUser.profilePhotoUrl,
        role: sessionUser.role,
      }));

      if (sessionUser.role === 'Administrator') {
        navigate('/admin-dashboard');
      } else {
        navigate('/user-dashboard');
      }
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'response' in err
        ? (err as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'Login failed. Please try again.'
        : 'Login failed. Please try again.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'radial-gradient(circle at top, rgba(56, 189, 248, 0.18), rgba(2, 8, 23, 1) 40%)',
      color: '#e2e8f0',
      padding: 20,
    }}>
      <div className="card" style={{ width: '100%', maxWidth: 460, padding: 28, borderRadius: 24 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 }}>
          <div style={{ width: 48, height: 48, borderRadius: 14, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'linear-gradient(135deg, #22c55e, #38bdf8)', color: '#08111f' }}>
            <ShieldCheck size={24} />
          </div>
          <div>
            <div style={{ fontSize: 12, letterSpacing: 2, textTransform: 'uppercase', color: '#38bdf8' }}>SmartGuard</div>
            <h2 style={{ margin: 0, fontSize: 26 }}>Secure Login</h2>
          </div>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'grid', gap: 20 }}>
          <div style={{ display: 'grid', gap: 10 }}>
            <label style={{ color: '#cbd5e1', fontSize: 13 }}>Email</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, borderRadius: 12, border: '1px solid rgba(148,163,184,0.2)', background: 'rgba(15,23,42,0.8)', padding: '0.8rem 0.9rem' }}>
              <Mail size={18} color="#94a3b8" />
              <input
                type="email"
                name="username"
                autoComplete="username"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                style={{ background: 'transparent', border: 'none', outline: 'none', color: '#f8fafc', width: '100%' }}
              />
            </div>
          </div>

          <label className="remember-email-control">
            <input type="checkbox" checked={rememberEmail} onChange={event => setRememberEmail(event.target.checked)} />
            <span>Remember my email on this device</span>
          </label>

          <div style={{ display: 'grid', gap: 10 }}>
            <label style={{ color: '#cbd5e1', fontSize: 13 }}>Password</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, borderRadius: 12, border: '1px solid rgba(148,163,184,0.2)', background: 'rgba(15,23,42,0.8)', padding: '0.8rem 0.9rem' }}>
              <LockKeyhole size={18} color="#94a3b8" />
              <input
                type="password"
                name="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                style={{ background: 'transparent', border: 'none', outline: 'none', color: '#f8fafc', width: '100%' }}
              />
            </div>
          </div>

          {error ? (
            <div style={{ color: '#fca5a5', background: 'rgba(127, 29, 29, 0.35)', border: '1px solid rgba(248, 113, 113, 0.4)', padding: '0.7rem 0.8rem', borderRadius: 12, fontSize: 14 }}>
              {error}
            </div>
          ) : null}

          <button type="submit" disabled={isSubmitting} style={{ background: 'linear-gradient(135deg, #38bdf8, #22c55e)', color: '#03131e', border: 'none', borderRadius: 12, padding: '0.9rem 1rem', fontWeight: 700, cursor: isSubmitting ? 'wait' : 'pointer', opacity: isSubmitting ? 0.7 : 1 }}>
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>

        <div style={{ marginTop: 18, color: '#94a3b8', fontSize: 13, textAlign: 'center' }}>
          Need an account? <Link to="/register" style={{ color: '#7dd3fc', fontWeight: 700 }}>Create one</Link><br />
          Demo admin: admin@smartguard.com / admin123<br />
          Demo user: user@smartguard.com / smartguard123
        </div>
      </div>
    </div>
  );
}
