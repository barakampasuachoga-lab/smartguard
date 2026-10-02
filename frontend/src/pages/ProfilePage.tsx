import { useEffect, useState } from 'react';
import { ImagePlus, Upload } from 'lucide-react';
import { getProfile, getProfilePhotoUrl, updateProfile, updateProfilePhoto } from '../api';
import type { AuthUser } from '../types';

export default function ProfilePage() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => () => {
    if (photoPreview) URL.revokeObjectURL(photoPreview);
  }, [photoPreview]);

  useEffect(() => {
    void getProfile().then(profile => {
      setUser(profile);
      setFullName(profile.fullName);
      setEmail(profile.email);
    }).catch(() => setError('Unable to load your profile.'));
  }, []);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError('');
    setMessage('');
    setSaving(true);

    try {
      const response = await updateProfile({ fullName, email, currentPassword, newPassword });
      localStorage.setItem('smartguard-token', response.token);
      localStorage.setItem('smartguard-user', JSON.stringify(response.user));
      setUser(response.user);
      setCurrentPassword('');
      setNewPassword('');
      setMessage('Profile updated.');
    } catch (failure: unknown) {
      const detail = failure && typeof failure === 'object' && 'response' in failure
        ? (failure as { response?: { data?: { message?: string } } }).response?.data?.message
        : undefined;
      setError(detail ?? 'Unable to update your profile.');
    } finally {
      setSaving(false);
    }
  };

  const handlePhotoChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    setError('');
    setPhotoFile(null);
    setPhotoPreview('');
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      setError('Choose a JPEG, PNG, or WebP photo no larger than 5 MB.');
      event.target.value = '';
      return;
    }
    setPhotoFile(file);
    setPhotoPreview(URL.createObjectURL(file));
  };

  const savePhoto = async () => {
    if (!photoFile) return;
    setSaving(true);
    setError('');
    setMessage('');
    try {
      const updatedUser = await updateProfilePhoto(photoFile);
      setUser(updatedUser);
      const storedUser = JSON.parse(localStorage.getItem('smartguard-user') ?? '{}') as Record<string, unknown>;
      localStorage.setItem('smartguard-user', JSON.stringify({ ...storedUser, profilePhotoUrl: updatedUser.profilePhotoUrl }));
      window.dispatchEvent(new Event('smartguard-profile-updated'));
      setPhotoFile(null);
      setPhotoPreview('');
      setMessage('Profile photo updated.');
    } catch (failure: unknown) {
      const detail = failure && typeof failure === 'object' && 'response' in failure
        ? (failure as { response?: { data?: { message?: string } } }).response?.data?.message
        : undefined;
      setError(detail ?? 'Unable to update your profile photo.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="profile-page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Account</p>
          <h2>Profile settings</h2>
        </div>
        {user && <span className="role-tag">{user.role}</span>}
      </div>
      <form className="profile-form" onSubmit={handleSubmit}>
        <div className="profile-photo-editor">
          <div className="profile-photo-preview">{(photoPreview || user?.profilePhotoUrl) ? <img src={photoPreview || getProfilePhotoUrl(user?.profilePhotoUrl) || ''} alt="Profile" /> : <ImagePlus size={24} />}</div>
          <div className="profile-photo-copy"><strong>Profile photo</strong><span>JPEG, PNG, or WebP · max 5 MB</span><label className="photo-select-button" htmlFor="profile-photo-file"><Upload size={14} /> Choose photo</label><input className="visually-hidden" id="profile-photo-file" type="file" accept="image/jpeg,image/png,image/webp" onChange={handlePhotoChange} /></div>
          {photoFile && <button className="primary-button photo-save-button" type="button" onClick={() => void savePhoto()} disabled={saving}>{saving ? 'Uploading…' : 'Save photo'}</button>}
        </div>
        <label>Full name<input value={fullName} onChange={event => setFullName(event.target.value)} required /></label>
        <label>Email address<input type="email" value={email} onChange={event => setEmail(event.target.value)} required /></label>
        <div className="form-divider"><span>Password change</span><small>Leave both fields blank to keep your current password.</small></div>
        <label>Current password<input type="password" autoComplete="current-password" value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></label>
        <label>New password<input type="password" autoComplete="new-password" minLength={8} value={newPassword} onChange={event => setNewPassword(event.target.value)} /></label>
        {error && <p className="form-error" role="alert">{error}</p>}
        {message && <p className="form-success" role="status">{message}</p>}
        <button className="primary-button" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save profile'}</button>
      </form>
    </section>
  );
}