import { useEffect, useState } from 'react';
import { Building2, ImagePlus, Pencil, Plus, Trash2, Upload, X } from 'lucide-react';
import { createProperty, deleteProperty, getProperties, getPropertyPhotoUrl, getUsers, updateProperty } from '../api';
import type { AuthUser, Property, PropertyStatus } from '../types';

export default function PropertiesPage() {
  const [properties, setProperties] = useState<Property[]>([]);
  const [name, setName] = useState('');
  const [address, setAddress] = useState('');
  const [owner, setOwner] = useState('');
  const [ownerUserId, setOwnerUserId] = useState('');
  const [users, setUsers] = useState<AuthUser[]>([]);
  const [propertyPhoto, setPropertyPhoto] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState('');
  const [editingPhotoUrl, setEditingPhotoUrl] = useState<string | null>(null);
  const [status, setStatus] = useState<PropertyStatus>('Home');
  const [error, setError] = useState('');
  const [loadError, setLoadError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const sessionUser = JSON.parse(localStorage.getItem('smartguard-user') ?? '{}') as { fullName?: string; name?: string; role?: string };
  const currentUserName = sessionUser.fullName ?? sessionUser.name ?? '';
  const isAdmin = sessionUser.role === 'Administrator';

  const loadProperties = async () => {
    const data = await getProperties();
    setProperties(data);
  };

  useEffect(() => {
    void loadProperties().catch(() => setLoadError('Unable to load properties.'));
    if (isAdmin) void getUsers().then(setUsers).catch(() => setLoadError('Unable to load user accounts for property assignment.'));
  }, []);

  useEffect(() => () => {
    if (photoPreview) URL.revokeObjectURL(photoPreview);
  }, [photoPreview]);

  const handlePhotoChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    setError('');
    setPropertyPhoto(null);
    setPhotoPreview('');
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      setError('Choose a JPEG, PNG, or WebP property image no larger than 5 MB.');
      event.target.value = '';
      return;
    }
    setPropertyPhoto(file);
    setPhotoPreview(URL.createObjectURL(file));
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!name.trim() || !address.trim() || (isAdmin && (!owner.trim() || !ownerUserId)) || (!editingId && !propertyPhoto)) {
      setError('Complete all property details and upload a property image.');
      return;
    }

    try {
      setIsSubmitting(true);
      setError('');
      const payload = { name: name.trim(), address: address.trim(), owner: isAdmin ? owner.trim() : currentUserName, ownerUserId: isAdmin ? Number(ownerUserId) : undefined, status };
      if (editingId) {
        const updated = await updateProperty(editingId, payload, propertyPhoto);
        setProperties(current => current.map(property => property.id === editingId ? updated : property));
      } else {
        const created = await createProperty(payload, propertyPhoto!);
        setProperties(current => [created, ...current]);
      }
      setName('');
      setAddress('');
      setOwner(isAdmin ? '' : currentUserName);
      setOwnerUserId('');
      setStatus('Home');
      setEditingId(null);
      setPropertyPhoto(null);
      setPhotoPreview('');
      setEditingPhotoUrl(null);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'response' in err
        ? (err as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'Unable to add property.'
        : 'Unable to add property.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const startEdit = (property: Property) => {
    setEditingId(property.id);
    setName(property.name);
    setAddress(property.address);
    setOwner(property.owner);
    setOwnerUserId(property.ownerUserId ? String(property.ownerUserId) : '');
    setStatus(property.status);
    setEditingPhotoUrl(property.photoUrl ?? null);
    setPropertyPhoto(null);
    setPhotoPreview('');
    setError('');
  };

  const cancelEdit = () => {
    setEditingId(null);
    setName('');
    setAddress('');
    setOwner(isAdmin ? '' : currentUserName);
    setOwnerUserId('');
    setStatus('Home');
    setEditingPhotoUrl(null);
    setPropertyPhoto(null);
    setPhotoPreview('');
    setError('');
  };

  const handleDelete = async (property: Property) => {
    if (!window.confirm(`Delete ${property.name} and its related records?`)) return;
    try {
      await deleteProperty(property.id);
      setProperties(current => current.filter(item => item.id !== property.id));
    } catch {
      setError('Unable to delete this property.');
    }
  };

  return (
    <div className="properties-page">
      <section className="workspace-section">
        <div className="workspace-section-heading"><div><p className="eyebrow">Property portfolio</p><h2>{editingId ? 'Edit property' : 'Add a property'}</h2></div>{editingId && <button className="subtle-button" onClick={cancelEdit}><X size={15} /> Cancel</button>}</div>
        <form className="property-form" onSubmit={handleSubmit}>
          <label>Property name<input value={name} onChange={(e) => setName(e.target.value)} placeholder="Riverside Villa" required /></label>
          <label>Address<input value={address} onChange={(e) => setAddress(e.target.value)} placeholder="18 River Road" required /></label>
          {isAdmin && <label>Assign owner<select value={ownerUserId} onChange={(event) => { const selected = users.find(user => user.id === Number(event.target.value)); setOwnerUserId(event.target.value); setOwner(selected?.fullName ?? ''); }} required><option value="">Choose a user</option>{users.filter(user => user.role !== 'Administrator').map(user => <option key={user.id} value={user.id}>{user.fullName} · {user.email}</option>)}</select></label>}
          <label>Security status<select value={status} onChange={(e) => setStatus(e.target.value as PropertyStatus)}>
              <option value="Home">Home</option>
              <option value="Away">Away</option>
              <option value="Vacation">Vacation</option>
              <option value="Maintenance">Maintenance</option>
            </select></label>
          <div className="property-photo-field">
            <label htmlFor="property-photo-file">Property image <span>{editingId ? 'Optional replacement' : 'Required'} · JPEG, PNG, or WebP · max 5 MB</span></label>
            <div className="property-photo-control">
              {(photoPreview || editingPhotoUrl) ? <img src={photoPreview || getPropertyPhotoUrl(editingPhotoUrl) || ''} alt="Property preview" /> : <span className="property-photo-placeholder"><ImagePlus size={21} /></span>}
              <div><strong>{propertyPhoto?.name ?? (editingPhotoUrl ? 'Current property image' : 'Add a property photo')}</strong><small>{propertyPhoto ? `${(propertyPhoto.size / 1024 / 1024).toFixed(1)} MB` : 'Show the property as it really looks.'}</small></div>
              <label className="photo-select-button" htmlFor="property-photo-file"><Upload size={14} /> {editingId ? 'Replace' : 'Choose'}</label>
              <input id="property-photo-file" className="visually-hidden" type="file" accept="image/jpeg,image/png,image/webp" required={!editingId && !propertyPhoto} onChange={handlePhotoChange} />
            </div>
          </div>
          <div className="property-form-actions"><button className="primary-button" type="submit" disabled={isSubmitting}>{editingId ? <Pencil size={15} /> : <Plus size={15} />}{isSubmitting ? 'Saving…' : editingId ? 'Save changes' : 'Add property'}</button></div>
        </form>
        {error && <p className="form-error" role="alert">{error}</p>}
      </section>

      <section className="workspace-section">
        <div className="workspace-section-heading"><div><p className="eyebrow">Portfolio</p><h2>Your properties</h2></div><span className="count-label">{properties.length} properties</span></div>
        {loadError && <p className="form-error" role="alert">{loadError}</p>}
        {!properties.length && !loadError ? <div className="empty-state"><Building2 size={21} /><p>No properties have been added yet.</p></div> : <div className="property-list">
          {properties.map(property => (
            <article className="property-row" key={property.id}>
              <div className="property-name">{property.photoUrl ? <img className="property-row-photo" src={getPropertyPhotoUrl(property.photoUrl) ?? undefined} alt={`${property.name}`} /> : <span className="property-icon"><Building2 size={17} /></span>}<div><strong>{property.name}</strong><span>{property.address}</span></div></div>
              <div className="property-owner"><span>Owner</span><strong>{property.owner}</strong></div>
              <div className="property-status"><span>Status</span><strong>{property.status}</strong></div>
              <div className="property-actions"><button className="icon-button edit-property" onClick={() => startEdit(property)} title={`Edit ${property.name}`} aria-label={`Edit ${property.name}`}><Pencil size={16} /></button>{isAdmin && <button className="icon-button delete-property" onClick={() => void handleDelete(property)} title={`Delete ${property.name}`} aria-label={`Delete ${property.name}`}><Trash2 size={15} /></button>}</div>
            </article>
          ))}
        </div>}
      </section>
    </div>
  );
}
