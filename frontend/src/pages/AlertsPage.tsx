import { Bell, Check, CircleCheck, Clock3, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { acknowledgeAlert, getAlert, getAlerts, resolveAlert } from '../api';
import type { AlertItem } from '../types';

type AlertFilter = 'All' | 'Unread' | 'Acknowledged' | 'High' | 'Critical' | 'Resolved';
const filters: AlertFilter[] = ['All', 'Unread', 'Acknowledged', 'High', 'Critical', 'Resolved'];

export default function AlertsPage() {
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [selected, setSelected] = useState<AlertItem | null>(null);
  const [activeFilter, setActiveFilter] = useState<AlertFilter>('All');
  const [acknowledgementComment, setAcknowledgementComment] = useState('');
  const [resolutionNote, setResolutionNote] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void getAlerts().then(setAlerts).catch(() => setError('Unable to load alerts. Check your connection and sign in again if your session expired.'));
  }, []);

  const visibleAlerts = useMemo(() => alerts.filter(alert => {
    if (activeFilter === 'Unread') return alert.status === 'Unread';
    if (activeFilter === 'Acknowledged') return alert.status === 'Acknowledged';
    if (activeFilter === 'Resolved') return alert.status === 'Resolved';
    if (activeFilter === 'High') return alert.priority === 'High';
    if (activeFilter === 'Critical') return alert.priority === 'Critical';
    return true;
  }), [alerts, activeFilter]);

  const openAlert = async (alert: AlertItem) => {
    setError('');
    try {
      const detail = await getAlert(alert.id);
      setSelected(detail);
      setAlerts(current => current.map(item => item.id === detail.id ? { ...item, ...detail } : item));
      setAcknowledgementComment(detail.acknowledgementComment ?? '');
      setResolutionNote(detail.resolutionNote ?? '');
    } catch {
      setError('Unable to load this alert’s details.');
    }
  };

  const handleAcknowledge = async () => {
    if (!selected) return;
    try {
      setBusy(true); setError('');
      const updated = await acknowledgeAlert(selected.id, acknowledgementComment);
      const next = { ...selected, ...updated };
      setSelected(next);
      setAlerts(current => current.map(item => item.id === next.id ? { ...item, ...next } : item));
    } catch {
      setError('Unable to acknowledge this alert.');
    } finally { setBusy(false); }
  };

  const handleResolve = async () => {
    if (!selected) return;
    if (!resolutionNote.trim()) { setError('Add an investigation note before resolving this alert.'); return; }
    try {
      setBusy(true); setError('');
      const updated = await resolveAlert(selected.id, resolutionNote);
      const next = { ...selected, ...updated };
      setSelected(next);
      setAlerts(current => current.map(item => item.id === next.id ? { ...item, ...next } : item));
    } catch (cause) {
      const message = cause && typeof cause === 'object' && 'response' in cause
        ? (cause as { response?: { data?: { message?: string } } }).response?.data?.message
        : undefined;
      setError(message ?? 'Unable to resolve this alert.');
    } finally { setBusy(false); }
  };

  const acknowledgeCount = alerts.filter(alert => alert.status === 'Acknowledged').length;
  const unresolvedCount = alerts.filter(alert => alert.status !== 'Resolved').length;

  return (
    <>
      <section className="workspace-section alerts-workspace">
        <div className="workspace-section-heading"><div><p className="eyebrow">PRIORITY QUEUE</p><h2>Alerts</h2></div><span className="count-label">{unresolvedCount} open · {acknowledgeCount} acknowledged</span></div>
        {error && <p className="form-error" role="alert">{error}</p>}
        <div className="alert-filter-row" role="group" aria-label="Filter alerts">{filters.map(filter => <button type="button" key={filter} className={`alert-filter ${activeFilter === filter ? 'active' : ''}`} onClick={() => setActiveFilter(filter)}>{filter}<span>{filter === 'All' ? alerts.length : filter === 'Unread' ? alerts.filter(item => item.status === 'Unread').length : filter === 'Acknowledged' ? acknowledgeCount : filter === 'Resolved' ? alerts.filter(item => item.status === 'Resolved').length : alerts.filter(item => item.priority === filter).length}</span></button>)}</div>
        <div className="alert-list">
          {visibleAlerts.map(alert => <article className={`alert-row ${selected?.id === alert.id ? 'alert-row-selected' : ''}`} key={alert.id}>
            <div className="alert-symbol"><Bell size={17} /></div>
            <button className="alert-copy alert-open-button" type="button" onClick={() => void openAlert(alert)}><div className="alert-meta"><strong className={`priority-label priority-text-${alert.priority.toLowerCase()}`}>{alert.priority} · {alert.alertType ?? 'Security alert'}</strong><time>{new Date(alert.createdAt).toLocaleString()}</time></div><p>{alert.message}</p><span>{alert.propertyName ?? `Property ${alert.propertyId}`} · {alert.securityEvent?.location ?? alert.securityEvent?.deviceId ?? 'Open for details'}</span></button>
            <span className={`alert-status status-${alert.status.toLowerCase()}`}>{alert.status}</span>
            <button type="button" className="subtle-button alert-details-button" onClick={() => void openAlert(alert)}>Details</button>
          </article>)}
        </div>
        {!visibleAlerts.length && !error && <div className="empty-state">No {activeFilter === 'All' ? '' : activeFilter.toLowerCase() + ' '}alerts to show.</div>}
      </section>

      {selected && <div className="alert-drawer-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setSelected(null); }}>
        <aside className="alert-detail-drawer" role="dialog" aria-modal="true" aria-labelledby="alert-detail-title">
          <header className="alert-drawer-header"><div><p className="eyebrow">ALERT DETAILS</p><h2 id="alert-detail-title">{selected.alertType ?? 'Security alert'}</h2></div><button className="icon-button" type="button" aria-label="Close alert details" onClick={() => setSelected(null)}><X size={18} /></button></header>
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="alert-detail-badges"><span className={`priority-label priority-text-${selected.priority.toLowerCase()}`}>{selected.priority} priority</span><span className={`alert-status status-${selected.status.toLowerCase()}`}>{selected.status}</span></div>
          <p className="alert-detail-reason">{selected.message}</p>
          <div className="alert-detail-facts">
            <div><span>Property</span><strong>{selected.propertyName ?? selected.propertyId}</strong></div>
            <div><span>Location</span><strong>{selected.securityEvent?.location || 'Not specified'}</strong></div>
            <div><span>Event</span><strong>{selected.securityEvent?.eventType ?? 'Security event'}</strong></div>
            <div><span>Device</span><strong>{selected.securityEvent?.deviceId ?? 'Not specified'}</strong></div>
            <div><span>Occurred</span><strong>{selected.securityEvent ? new Date(selected.securityEvent.timestamp).toLocaleString() : new Date(selected.createdAt).toLocaleString()}</strong></div>
            <div><span>Alert ID</span><strong className="alert-id-value">{selected.id}</strong></div>
          </div>
          <div className="alert-lifecycle"><span className={selected.status !== 'Unread' ? 'complete' : 'current'}>Created</span><i /><span className={['Read', 'Acknowledged', 'Resolved'].includes(selected.status) ? 'complete' : ''}>Read</span><i /><span className={['Acknowledged', 'Resolved'].includes(selected.status) ? 'complete' : ''}>Acknowledged</span><i /><span className={selected.status === 'Resolved' ? 'complete' : ''}>Resolved</span></div>
          {selected.acknowledgedAt && <div className="alert-audit-entry"><Check size={14} /><div><strong>Acknowledged by {selected.acknowledgedBy ?? 'account user'}</strong><span><Clock3 size={12} />{new Date(selected.acknowledgedAt).toLocaleString()}</span>{selected.acknowledgementComment && <p>{selected.acknowledgementComment}</p>}</div></div>}
          {selected.resolvedAt && <div className="alert-audit-entry"><CircleCheck size={14} /><div><strong>Resolved by {selected.resolvedBy ?? 'account user'}</strong><span><Clock3 size={12} />{new Date(selected.resolvedAt).toLocaleString()}</span>{selected.resolutionNote && <p>{selected.resolutionNote}</p>}</div></div>}
          {selected.escalatedAt && <div className="alert-audit-entry alert-escalation-entry"><Bell size={14} /><div><strong>Escalation triggered</strong><span><Clock3 size={12} />{new Date(selected.escalatedAt).toLocaleString()}</span>{selected.escalationDetails && <p>{selected.escalationDetails}</p>}</div></div>}
          {selected.status !== 'Acknowledged' && selected.status !== 'Resolved' && <div className="alert-action-form"><label htmlFor="ack-comment">Acknowledgement comment <small>Optional</small></label><textarea id="ack-comment" rows={3} value={acknowledgementComment} onChange={event => setAcknowledgementComment(event.target.value)} placeholder="Add context for the activity…" /><button className="primary-button" type="button" disabled={busy} onClick={() => void handleAcknowledge()}><Check size={14} />{busy ? 'Saving…' : 'Acknowledge alert'}</button></div>}
          {selected.status === 'Acknowledged' && <div className="alert-action-form"><label htmlFor="resolution-note">Investigation / resolution note <small>Required</small></label><textarea id="resolution-note" rows={3} value={resolutionNote} onChange={event => setResolutionNote(event.target.value)} placeholder="Record what you found…" required /><button className="primary-button" type="button" disabled={busy} onClick={() => void handleResolve()}><CircleCheck size={14} />{busy ? 'Saving…' : 'Resolve alert'}</button></div>}
        </aside>
      </div>}
    </>
  );
}
