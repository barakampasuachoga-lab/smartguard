import { useEffect, useState } from 'react';
import { Bell, Check, CircleCheck, RotateCcw } from 'lucide-react';
import { acknowledgeAlert, getAlerts, resolveAlert } from '../api';
import type { AlertItem } from '../types';

export default function AlertsPage() {
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [error, setError] = useState('');
  const isAdmin = JSON.parse(localStorage.getItem('smartguard-user') ?? '{}').role === 'Administrator';

  useEffect(() => {
    void getAlerts().then(setAlerts).catch(() => setError('Unable to load alerts.'));
  }, []);

  const handleAcknowledge = async (id: string) => {
    try {
      await acknowledgeAlert(id);
      setAlerts(current => current.map(item => item.id === id ? { ...item, status: 'Acknowledged' } : item));
    } catch {
      setError('Unable to update this alert.');
    }
  };

  const handleResolve = async (id: string) => {
    try {
      await resolveAlert(id);
      setAlerts(current => current.map(item => item.id === id ? { ...item, status: 'Resolved' } : item));
    } catch {
      setError('Unable to resolve this alert.');
    }
  };

  return (
    <section className="workspace-section">
      <div className="workspace-section-heading"><div><p className="eyebrow">Priority queue</p><h2>Alerts</h2></div><span className="count-label">{alerts.length} alerts</span></div>
      {error && <p className="form-error" role="alert">{error}</p>}
      <div className="alert-list">
        {alerts.map(alert => (
          <article className="alert-row" key={alert.id}>
            <div className="alert-symbol"><Bell size={17} /></div>
            <div className="alert-copy"><div className="alert-meta"><strong className={`priority-label priority-text-${alert.priority.toLowerCase()}`}>{alert.priority} priority</strong><time>{new Date(alert.createdAt).toLocaleString()}</time></div><p>{alert.message}</p><span>Property {alert.propertyId}</span></div>
            <span className={`alert-status status-${alert.status.toLowerCase()}`}>{alert.status}</span>
            <div className="alert-actions">
              {alert.status !== 'Acknowledged' && alert.status !== 'Resolved' && <button className="subtle-button" onClick={() => void handleAcknowledge(alert.id)}><Check size={14} /> Mark reviewed</button>}
              {isAdmin && alert.status !== 'Resolved' && <button className="icon-button resolve-button" onClick={() => void handleResolve(alert.id)} title="Resolve alert" aria-label="Resolve alert"><CircleCheck size={17} /></button>}
              {alert.status === 'Resolved' && <span className="resolved-mark" title="Resolved"><RotateCcw size={15} /></span>}
            </div>
          </article>
        ))}
      </div>
      {!alerts.length && !error && <div className="empty-state">No alerts to review.</div>}
    </section>
  );
}
