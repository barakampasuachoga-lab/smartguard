import { ImagePlus, LockKeyhole, Mail, ShieldCheck, UserRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { registerUser } from '../api';

export default function RegisterPage() {
  const navigate = useNavigate();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [profilePhoto, setProfilePhoto] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => () => {
    if (photoPreview) URL.revokeObjectURL(photoPreview);
  }, [photoPreview]);

  const handlePhotoChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    setError('');
    setPhotoPreview('');
    setProfilePhoto(null);
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setError('Choose a JPEG, PNG, or WebP photo.');
      event.target.value = '';
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('Profile photos must be 5 MB or smaller.');
      event.target.value = '';
      return;
    }
    setProfilePhoto(file);
    setPhotoPreview(URL.createObjectURL(file));
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!fullName.trim() || !email || !password || !confirmPassword || !profilePhoto) {
      setError('Complete all fields and upload a profile photo.');
      setSuccess('');
      return;
    }

    if (!email.includes('@')) {
      setError('Please use a valid email address.');
      setSuccess('');
      return;
    }

    if (password.length < 8) {
      setError('Password must be at least 8 characters long.');
      setSuccess('');
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      setSuccess('');
      return;
    }

    try {
      setIsSubmitting(true);
      setError('');
      const response = await registerUser({ fullName: fullName.trim(), email, password }, profilePhoto);
      localStorage.setItem('smartguard-remembered-email', email.trim());
      setSuccess(response.message || 'Account created successfully.');

      setTimeout(() => {
        navigate('/login', { replace: true });
      }, 1200);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'response' in err
        ? (err as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'Account creation failed.'
        : 'Account creation failed.';
      setError(message);
      setSuccess('');
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
      <div className="card" style={{ width: '100%', maxWidth: 480, padding: 28, borderRadius: 24 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 }}>
          <div style={{ width: 48, height: 48, borderRadius: 14, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'linear-gradient(135deg, #22c55e, #38bdf8)', color: '#08111f' }}>
            <ShieldCheck size={24} />
          </div>
          <div>
            <div style={{ fontSize: 12, letterSpacing: 2, textTransform: 'uppercase', color: '#38bdf8' }}>SmartGuard</div>
            <h2 style={{ margin: 0, fontSize: 26 }}>Create account</h2>
          </div>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'grid', gap: 18 }}>
          <div style={{ display: 'grid', gap: 10 }}>
            <label style={{ color: '#cbd5e1', fontSize: 13 }}>Full name</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, borderRadius: 12, border: '1px solid rgba(148,163,184,0.2)', background: 'rgba(15,23,42,0.8)', padding: '0.8rem 0.9rem' }}>
              <UserRound size={18} color="#94a3b8" />
              <input
                type="text"
                required
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder="Jane Doe"
                style={{ background: 'transparent', border: 'none', outline: 'none', color: '#f8fafc', width: '100%' }}
              />
            </div>
          </div>

          <div style={{ display: 'grid', gap: 10 }}>
            <label style={{ color: '#cbd5e1', fontSize: 13 }}>Email</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, borderRadius: 12, border: '1px solid rgba(148,163,184,0.2)', background: 'rgba(15,23,42,0.8)', padding: '0.8rem 0.9rem' }}>
              <Mail size={18} color="#94a3b8" />
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                style={{ background: 'transparent', border: 'none', outline: 'none', color: '#f8fafc', width: '100%' }}
              />
            </div>
          </div>

          <div style={{ display: 'grid', gap: 10 }}>
            <label style={{ color: '#cbd5e1', fontSize: 13 }}>Password</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, borderRadius: 12, border: '1px solid rgba(148,163,184,0.2)', background: 'rgba(15,23,42,0.8)', padding: '0.8rem 0.9rem' }}>
              <LockKeyhole size={18} color="#94a3b8" />
              <input
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                style={{ background: 'transparent', border: 'none', outline: 'none', color: '#f8fafc', width: '100%' }}
              />
            </div>
          </div>

          <div style={{ display: 'grid', gap: 10 }}>
            <label style={{ color: '#cbd5e1', fontSize: 13 }}>Confirm password</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, borderRadius: 12, border: '1px solid rgba(148,163,184,0.2)', background: 'rgba(15,23,42,0.8)', padding: '0.8rem 0.9rem' }}>
              <LockKeyhole size={18} color="#94a3b8" />
              <input
                type="password"
                required
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Repeat your password"
                style={{ background: 'transparent', border: 'none', outline: 'none', color: '#f8fafc', width: '100%' }}
              />
            </div>
          </div>

          <div className="register-photo-field">
            <label htmlFor="register-profile-photo">Profile photo <span>Required · JPEG, PNG, or WebP · max 5 MB</span></label>
            <div className="register-photo-control">
              {photoPreview ? <img src={photoPreview} alt="Selected profile preview" /> : <span className="register-photo-placeholder"><ImagePlus size={22} /></span>}
              <div><strong>{profilePhoto?.name ?? 'Choose a profile photo'}</strong><small>{profilePhoto ? `${(profilePhoto.size / 1024 / 1024).toFixed(1)} MB` : 'A clear face photo helps identify your account.'}</small></div>
              <label className="photo-select-button" htmlFor="register-profile-photo">Browse</label>
              <input id="register-profile-photo" className="visually-hidden" type="file" accept="image/jpeg,image/png,image/webp" required onChange={handlePhotoChange} />
            </div>
          </div>

          {error ? (
            <div style={{ color: '#fca5a5', background: 'rgba(127, 29, 29, 0.35)', border: '1px solid rgba(248, 113, 113, 0.4)', padding: '0.7rem 0.8rem', borderRadius: 12, fontSize: 14 }}>
              {error}
            </div>
          ) : null}

          {success ? (
            <div style={{ color: '#bbf7d0', background: 'rgba(20, 83, 45, 0.35)', border: '1px solid rgba(34, 197, 94, 0.4)', padding: '0.7rem 0.8rem', borderRadius: 12, fontSize: 14 }}>
              {success}
            </div>
          ) : null}

          <button type="submit" disabled={isSubmitting} style={{ background: 'linear-gradient(135deg, #38bdf8, #22c55e)', color: '#03131e', border: 'none', borderRadius: 12, padding: '0.9rem 1rem', fontWeight: 700, cursor: isSubmitting ? 'wait' : 'pointer', opacity: isSubmitting ? 0.7 : 1 }}>
            {isSubmitting ? 'Creating account...' : 'Create account'}
          </button>
        </form>

        <div style={{ marginTop: 18, color: '#94a3b8', fontSize: 13, textAlign: 'center' }}>
          Already have an account? <Link to="/login" style={{ color: '#7dd3fc', fontWeight: 700 }}>Sign in</Link>
        </div>
      </div>
    </div>
  );
}
