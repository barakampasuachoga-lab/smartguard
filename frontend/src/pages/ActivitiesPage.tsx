import { useEffect, useState } from 'react';
import { Check, Clock3 } from 'lucide-react';
import { getEvents, reviewEvent } from '../api';
import type { SecurityEvent } from '../types';

export default function ActivitiesPage() {
  const [events, setEvents] = useState<SecurityEvent[]>([]);
  const [error, setError] = useState('');
  const isAdmin = JSON.parse(localStorage.getItem('smartguard-user') ?? '{}').role === 'Administrator';

  useEffect(() => {
    void getEvents().then(setEvents).catch(() => setError('Unable to load security events.'));
  }, []);

  const handleReview = async (id: string) => {
    try {
      const updated = await reviewEvent(id);
      setEvents(current => current.map(event => event.id === id ? updated : event));
    } catch {
      setError('Unable to mark this incident as under review.');
    }
  };

  return (
    <section className="workspace-section">
      <div className="workspace-section-heading"><div><p className="eyebrow">Incident timeline</p><h2>Security events</h2></div><span className="count-label">{events.length} events</span></div>
      {error && <p className="form-error" role="alert">{error}</p>}
      <div className="event-list">
        {events.map(event => (
          <article className="event-row" key={event.id}>
            <div className="event-kind"><span className={`priority-dot priority-${event.priority.toLowerCase()}`} /><div><strong>{event.eventType}</strong><span>{event.priority} priority</span></div></div>
            <div className="event-description"><strong>{event.location}</strong><span>{event.description}</span>
            </div>
            <div className="event-device"><span>{event.sensorType}</span><strong>{event.deviceId}</strong><small>{event.status}</small>
            </div>
            <time className="event-time"><Clock3 size={13} />{new Date(event.timestamp).toLocaleString()}</time>
            {isAdmin && event.status !== 'Reviewing' && <button className="subtle-button review-button" onClick={() => void handleReview(event.id)}><Check size={14} /> Review incident</button>}
          </article>
        ))}
      </div>
      {!events.length && !error && <div className="empty-state">No security events to show.</div>}
    </section>
  );
}
