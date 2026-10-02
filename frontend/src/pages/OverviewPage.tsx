import { Activity, AlertTriangle, Building2, ShieldAlert } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { getEvents, getOverview } from '../api';
import StatCard from '../components/StatCard';
import type { DashboardOverview, SecurityEvent } from '../types';

export default function OverviewPage() {
  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [events, setEvents] = useState<SecurityEvent[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    void Promise.all([getOverview(), getEvents()])
      .then(([summary, securityEvents]) => {
        setOverview(summary);
        setEvents(securityEvents);
      })
      .catch(() => setError('Unable to load analytics for your properties.'));
  }, []);

  const chartData = Array.from({ length: 7 }, (_, index) => {
    const day = new Date();
    day.setDate(day.getDate() - (6 - index));
    const count = events.filter(event => new Date(event.timestamp).toDateString() === day.toDateString()).length;
    return { name: day.toLocaleDateString(undefined, { weekday: 'short' }), value: count };
  });

  const totalProperties = overview?.totalProperties ?? 0;
  const totalEvents = overview?.totalEvents ?? 0;
  const openAlerts = overview?.openAlerts ?? 0;
  const totalDevices = overview?.totalDevices ?? 0;

  return (
    <div style={{ display: 'grid', gap: 20 }}>
      {error && <p className="form-error" role="alert">{error}</p>}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16 }}>
        <StatCard title="Properties" value={totalProperties} subtitle="Assigned to your account" accent="#22c55e" icon={<Building2 size={18} />} />
        <StatCard title="Security Events" value={totalEvents} subtitle="Total recorded activity" accent="#38bdf8" icon={<Activity size={18} />} />
        <StatCard title="Open Alerts" value={openAlerts} subtitle="Unread or read, awaiting review" accent="#f59e0b" icon={<AlertTriangle size={18} />} />
        <StatCard title="Risk Score" value={`${overview?.riskScore ?? 0}/100`} subtitle="Calculated from events and alerts" accent="#f43f5e" icon={<ShieldAlert size={18} />} />
      </div>

      <div className="card" style={{ padding: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 18 }}>
          <h3 style={{ margin: 0 }}>Event volume · last 7 days</h3>
          <span style={{ color: '#94a3b8', fontSize: 13 }}>{totalDevices} connected devices</span>
        </div>
        <div style={{ height: 260 }}>
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={chartData}>
              <defs>
                <linearGradient id="threatFill" x1="0" x2="0" y1="0" y2="1">
                  <stop offset="5%" stopColor="#38bdf8" stopOpacity={0.7} />
                  <stop offset="95%" stopColor="#38bdf8" stopOpacity={0.08} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke="#334155" vertical={false} />
              <XAxis dataKey="name" stroke="#94a3b8" />
              <YAxis stroke="#94a3b8" />
              <Tooltip />
              <Area type="monotone" dataKey="value" stroke="#38bdf8" fill="url(#threatFill)" strokeWidth={3} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 0.8fr', gap: 20 }}>
        <div className="card" style={{ padding: 20 }}>
          <h3 style={{ marginTop: 0 }}>Recent activity</h3>
          <div style={{ display: 'grid', gap: 14 }}>
            {events.slice(0, 5).map(event => (
              <div key={event.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '0.9rem 1rem', borderRadius: 14, background: 'rgba(15,23,42,0.8)', border: '1px solid rgba(148,163,184,0.12)' }}>
                <div>
                  <div style={{ fontWeight: 600 }}>{event.eventType}</div>
                  <div style={{ color: '#94a3b8', fontSize: 13 }}>{event.location}</div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontWeight: 600 }}>{event.status}</div>
                  <div style={{ color: '#94a3b8', fontSize: 13 }}>{new Date(event.timestamp).toLocaleString()}</div>
                </div>
              </div>
            ))}
            {!events.length && !error && <div className="empty-state">No security events in the last 50 records.</div>}
          </div>
        </div>

        <div className="card" style={{ padding: 20 }}>
          <h3 style={{ marginTop: 0 }}>Priority alerts</h3>
          <div style={{ display: 'grid', gap: 12 }}>
            {(overview?.recentAlerts ?? []).map(alert => (
              <div key={alert.id} style={{ padding: '0.8rem 0.9rem', borderRadius: 14, background: 'rgba(15,23,42,0.8)', border: '1px solid rgba(148,163,184,0.12)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <span style={{ fontWeight: 600 }}>{alert.priority}</span>
                  <span style={{ color: '#94a3b8', fontSize: 12 }}>{alert.status}</span>
                </div>
                <div style={{ color: '#cbd5e1', marginTop: 6, fontSize: 14 }}>{alert.message}</div>
              </div>
            ))}
            {!overview?.recentAlerts?.length && <div className="empty-state">No alerts are currently linked to your properties.</div>}
          </div>
        </div>
      </div>
    </div>
  );
}
