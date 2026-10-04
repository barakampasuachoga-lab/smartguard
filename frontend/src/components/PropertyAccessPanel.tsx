import { Check, Clock3, Plus, Trash2, Users } from 'lucide-react';
import { useEffect, useState } from 'react';
import { addTrustedContact, createSecurityCheckIn, getSecurityCheckIns, getTrustedContacts, removeTrustedContact } from '../api';
import type { SecurityCheckIn, TrustedContact } from '../types';

export default function PropertyAccessPanel({ propertyId }: { propertyId: string }) {
  const [contacts, setContacts] = useState<TrustedContact[]>([]);
  const [checkIns, setCheckIns] = useState<SecurityCheckIn[]>([]);
  const [name, setName] = useState('');
  const [relationship, setRelationship] = useState('Family');
  const [email, setEmail] = useState('');
  const [days, setDays] = useState('Everyday');
  const [startTime, setStartTime] = useState('06:00');
  const [endTime, setEndTime] = useState('22:00');
  const [note, setNote] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const refresh = async () => {
    const [people, records] = await Promise.all([getTrustedContacts(propertyId), getSecurityCheckIns(propertyId)]);
    setContacts(people);
    setCheckIns(records);
  };

  useEffect(() => {
    if (!propertyId) return;
    setError('');
    void refresh().catch(() => setError('Unable to load trusted access information.'));
  }, [propertyId]);

  const saveContact = async (event: React.FormEvent) => {
    event.preventDefault();
    try {
      setSaving(true); setError('');
      const added = await addTrustedContact(propertyId, { name, relationship, email: email || null, days, startTime, endTime, isActive: true });
      setContacts(current => [...current, added].sort((left, right) => left.name.localeCompare(right.name)));
      setName(''); setEmail('');
    } catch { setError('Unable to save this trusted person.'); }
    finally { setSaving(false); }
  };

  const removeContact = async (contact: TrustedContact) => {
    try {
      await removeTrustedContact(propertyId, contact.id);
      setContacts(current => current.filter(item => item.id !== contact.id));
    } catch { setError('Unable to remove this trusted person.'); }
  };

  const checkIn = async () => {
    try {
      setSaving(true); setError('');
      const record = await createSecurityCheckIn(propertyId, note);
      setCheckIns(current => [record, ...current].slice(0, 30));
      setNote('');
    } catch { setError('Unable to record your security check-in.'); }
    finally { setSaving(false); }
  };

  return <section className="intel-panel property-access-panel">
    <div className="intel-section-heading"><div><p className="eyebrow">TRUSTED ACCESS & CHECK-IN</p><h3>People and property check-ins</h3></div><Users size={16} /></div>
    <p className="access-disclosure">Access windows describe expected activity. SmartGuard does not identify who entered.</p>
    <form className="trusted-contact-form" onSubmit={event => void saveContact(event)}>
      <input value={name} onChange={event => setName(event.target.value)} placeholder="Person’s name" aria-label="Person’s name" required />
      <select value={relationship} onChange={event => setRelationship(event.target.value)} aria-label="Relationship"><option>Family</option><option>Cleaner</option><option>Caretaker</option><option>Security personnel</option><option>Other</option></select>
      <input value={email} onChange={event => setEmail(event.target.value)} placeholder="Email (optional)" type="email" aria-label="Email address" />
      <select value={days} onChange={event => setDays(event.target.value)} aria-label="Authorized days"><option>Everyday</option><option>Monday-Friday</option><option>Weekends</option><option>Monday</option><option>Tuesday</option><option>Wednesday</option><option>Thursday</option><option>Friday</option><option>Saturday</option><option>Sunday</option></select>
      <input value={startTime} onChange={event => setStartTime(event.target.value)} type="time" aria-label="Access begins" />
      <input value={endTime} onChange={event => setEndTime(event.target.value)} type="time" aria-label="Access ends" />
      <button className="subtle-button" type="submit" disabled={saving}><Plus size={13} /> Add person</button>
    </form>
    {error && <p className="form-error" role="alert">{error}</p>}
    <div className="trusted-contact-list">{contacts.map(contact => <article className="trusted-contact-row" key={contact.id}><span className="trusted-person-avatar">{contact.name.trim().charAt(0).toUpperCase()}</span><div><strong>{contact.name}</strong><span>{contact.relationship}{contact.email ? ` · ${contact.email}` : ''}</span><small>{contact.days} · {contact.startTime}–{contact.endTime}</small></div><button className="icon-button" type="button" onClick={() => void removeContact(contact)} aria-label={`Remove ${contact.name}`}><Trash2 size={14} /></button></article>)}</div>
    {!contacts.length && <p className="schedule-empty">Add family, caretakers, cleaners, or assigned security personnel with their expected access hours.</p>}
    <div className="checkin-block"><div className="checkin-heading"><div><strong>Security check-in</strong><span>Confirm your status using your signed-in account.</span></div><button type="button" className="primary-button" onClick={() => void checkIn()} disabled={saving}><Check size={14} /> I’m safely inside</button></div><div className="checkin-note-row"><input value={note} onChange={event => setNote(event.target.value)} placeholder="Optional check-in note" aria-label="Optional check-in note" /></div>
      {checkIns.slice(0, 3).map(item => <article className="checkin-entry" key={item.id}><Clock3 size={13} /><span><strong>{item.userName}</strong>{item.note ? ` · ${item.note}` : ''}</span><time>{new Date(item.createdAt).toLocaleString()}</time></article>)}
    </div>
  </section>;
}
