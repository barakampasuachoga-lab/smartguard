import { useState } from 'react';
import { Link } from 'react-router-dom';
import { requestPasswordReset } from '../api';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setError('');
    setMessage('');
    try {
      const result = await requestPasswordReset(email);
      setMessage(result.message);
    } catch (err: unknown) {
      const response = err && typeof err === 'object' && 'response' in err
        ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
        : undefined;
      setError(response ?? 'We could not process your request. Please try again later.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 20, background: 'radial-gradient(circle at top, rgba(56, 189, 248, 0.18), rgba(2, 8, 23, 1) 40%)', color: '#e2e8f0' }}>
      <section className="card" style={{ width: '100%', maxWidth: 460, padding: 28, borderRadius: 24 }}>
        <div style={{ color: '#38bdf8', fontSize: 12, letterSpacing: 2, textTransform: 'uppercase' }}>SmartGuard</div>
        <h1 style={{ margin: '8px 0', fontSize: 26 }}>Forgot your password?</h1>
        <p style={{ color: '#94a3b8', lineHeight: 1.6 }}>Enter the email address on your account. If it matches an account, we’ll send a password reset link.</p>

        <form onSubmit={handleSubmit} style={{ display: 'grid', gap: 16, marginTop: 24 }}>
          <label style={{ display: 'grid', gap: 8, color: '#cbd5e1', fontSize: 13 }}>
            Email
            <input type="email" required autoComplete="email" value={email} onChange={event => setEmail(event.target.value)} placeholder="you@example.com" style={{ width: '100%', boxSizing: 'border-box', borderRadius: 10, border: '1px solid rgba(148,163,184,0.25)', padding: '0.85rem', background: 'rgba(15,23,42,0.8)', color: '#f8fafc' }} />
          </label>
          {error ? <div role="alert" style={{ color: '#fca5a5', background: 'rgba(127, 29, 29, 0.35)', padding: 12, borderRadius: 10 }}>{error}</div> : null}
          {message ? <div role="status" style={{ color: '#bbf7d0', background: 'rgba(20, 83, 45, 0.3)', padding: 12, borderRadius: 10 }}>{message}</div> : null}
          <button type="submit" disabled={isSubmitting} style={{ background: 'linear-gradient(135deg, #38bdf8, #22c55e)', color: '#03131e', border: 'none', borderRadius: 10, padding: '0.9rem 1rem', fontWeight: 700, cursor: isSubmitting ? 'wait' : 'pointer' }}>
            {isSubmitting ? 'Sending…' : 'Send reset link'}
          </button>
        </form>

        <div style={{ marginTop: 20, textAlign: 'center' }}><Link to="/login" style={{ color: '#7dd3fc' }}>Back to sign in</Link></div>
      </section>
    </main>
  );
}
