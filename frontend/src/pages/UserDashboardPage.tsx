import { Activity, ArrowRight, Bell, Building2, CircleAlert, ShieldCheck } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getEvents, getOverview } from '../api';
import type { DashboardOverview, SecurityEvent } from '../types';

export default function UserDashboardPage() {
  const user = JSON.parse(localStorage.getItem('smartguard-user') ?? '{}') as { fullName?: string; name?: string };
  const fullName = user.fullName ?? user.name;
  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [events, setEvents] = useState<SecurityEvent[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    void Promise.all([getOverview(), getEvents()])
      .then(([summary, recentEvents]) => {
        setOverview(summary);
        setEvents(recentEvents.slice(0, 5));
      })
      .catch(() => setError('Unable to load your property security status.'));
  }, []);

  const stats = [
    { label: 'My properties', value: overview?.totalProperties ?? '—', icon: <Building2 size={17} /> },
    { label: 'Security events', value: overview?.totalEvents ?? '—', icon: <Activity size={17} /> },
    { label: 'Open alerts', value: overview?.openAlerts ?? '—', icon: <Bell size={17} /> },
    { label: 'Risk score', value: overview ? `${overview.riskScore}/100` : '—', icon: <ShieldCheck size={17} /> },
  ];

  return (
    <div className="resident-dashboard">
      <section className="resident-welcome">
        <div><p className="eyebrow">Property owner workspace</p><h2>Welcome back, {fullName?.split(' ')[0] ?? 'there'}.</h2><p>Here is the latest security picture for your properties.</p></div>
        <Link className="primary-button" to="/properties"><Building2 size={15} /> Manage properties</Link>
      </section>
      {error && <p className="form-error" role="alert">{error}</p>}
      <div className="resident-metrics">
        {stats.map(stat => <article className="resident-metric" key={stat.label}><div><span>{stat.label}</span><strong>{stat.value}</strong></div><i>{stat.icon}</i></article>)}
      </div>
      <section className="resident-content-grid">
        <div className="workspace-section resident-events">
          <div className="workspace-section-heading"><div><p className="eyebrow">Recent activity</p><h2>Security events</h2></div><Link className="text-link" to="/activities">View all <ArrowRight size={14} /></Link></div>
          {events.map(event => <article className="resident-event-row" key={event.id}><span className={`priority-dot priority-${event.priority.toLowerCase()}`} /><div><strong>{event.eventType}</strong><span>{event.location} · {event.deviceId}</span></div><time>{new Date(event.timestamp).toLocaleString()}</time></article>)}
          {!events.length && !error && <div className="empty-state">No events have been recorded for your properties.</div>}
        </div>
        <aside className="resident-quick-links">
          <p className="eyebrow">Quick access</p><h2>Stay in control.</h2>
          <Link to="/alerts"><span><CircleAlert size={17} /> Review alerts</span><ArrowRight size={15} /></Link>
          <Link to="/overview"><span><ShieldCheck size={17} /> Security intelligence</span><ArrowRight size={15} /></Link>
          <Link to="/reports"><span><Activity size={17} /> View reports</span><ArrowRight size={15} /></Link>
          <Link to="/profile"><span><ShieldCheck size={17} /> Manage profile</span><ArrowRight size={15} /></Link>
        </aside>
      </section>
    </div>
  );
}
