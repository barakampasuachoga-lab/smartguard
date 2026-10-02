import { Activity, ArrowRightLeft, BellRing, Building2, Filter, Fingerprint, Mail, Send, ShieldCheck, Users, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { Link, useNavigate } from 'react-router-dom';
import { clearStoredSession, getAdminUserDetails, getDevices, getOverview, getProperties, getPropertyPhotoUrl, getProfilePhotoUrl, getUsers, sendUserEmail, sendUserReport, toggleUserBlock } from '../api';
import type { AdminUserDetails, AuthUser, DashboardOverview, DeviceSummary, Property } from '../types';

export default function AdminDashboardPage() {
  const navigate = useNavigate();
  const [users, setUsers] = useState<AuthUser[]>([]);
  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [devices, setDevices] = useState<DeviceSummary[]>([]);
  const [properties, setProperties] = useState<Property[]>([]);
  const [selectedUser, setSelectedUser] = useState<AdminUserDetails | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [reportTitle, setReportTitle] = useState('');
  const [reportBody, setReportBody] = useState('');
  const [reportMessage, setReportMessage] = useState('');
  const [sendingReport, setSendingReport] = useState(false);
  const [emailSubject, setEmailSubject] = useState('');
  const [emailBody, setEmailBody] = useState('');
  const [emailMessage, setEmailMessage] = useState('');
  const [emailError, setEmailError] = useState('');
  const [sendingEmail, setSendingEmail] = useState(false);
  const [filter, setFilter] = useState<'All' | 'Blocked' | 'Active'>('All');
  const [search, setSearch] = useState('');
  const [error, setError] = useState('');

  const loadUsers = async () => {
    const data = await getUsers();
    setUsers(data);
  };

  useEffect(() => {
    const storedAdmin = localStorage.getItem('smartguard-user');
    const token = localStorage.getItem('smartguard-token');

    if (!storedAdmin || !token) {
      clearStoredSession();
      navigate('/login', { replace: true });
      return;
    }

    const parsedAdmin = JSON.parse(storedAdmin) as { role?: string };
    if (parsedAdmin.role !== 'Administrator') {
      navigate('/user-dashboard', { replace: true });
      return;
    }

    void loadUsers();
    void getOverview().then(setOverview).catch(() => setError('Unable to load system statistics.'));
    void getDevices().then(setDevices).catch(() => setError('Unable to load IoT device status.'));
    void getProperties().then(setProperties).catch(() => setError('Unable to load property portfolio.'));
  }, [navigate]);

  const filteredUsers = useMemo(() => users.filter(user => {
    const matchesFilter = filter === 'All'
      ? true
      : filter === 'Blocked'
        ? user.isBlocked
        : !user.isBlocked;

    const matchesSearch = `${user.fullName} ${user.email} ${user.role}`.toLowerCase().includes(search.toLowerCase());
    return matchesFilter && matchesSearch;
  }), [filter, search, users]);

  const chartData = useMemo(() => [
    { name: 'Admins', value: users.filter(user => user.role === 'Administrator').length },
    { name: 'Residents', value: users.filter(user => user.role === 'Resident User').length },
    { name: 'Blocked', value: users.filter(user => user.isBlocked).length },
  ], [users]);

  const summary = useMemo(() => [
    { label: 'Total users', value: String(users.length), icon: <Users size={16} /> },
    { label: 'Properties', value: String(overview?.totalProperties ?? 0), icon: <Filter size={16} /> },
    { label: 'Security events', value: String(overview?.totalEvents ?? 0), icon: <BellRing size={16} /> },
    { label: 'Active devices', value: String(devices.length), icon: <Fingerprint size={16} /> },
  ], [devices.length, overview, users.length]);

  const handleToggleBlock = async (userId: number, isBlocked: boolean) => {
    await toggleUserBlock(userId, { isBlocked });
    await loadUsers();
  };

  const openUserDetails = async (user: AuthUser) => {
    setSelectedUser(null);
    setDetailError('');
    setReportMessage('');
    setEmailMessage('');
    setEmailError('');
    setDetailLoading(true);
    try {
      setSelectedUser(await getAdminUserDetails(user.id));
    } catch {
      setDetailError('Unable to load this user profile.');
    } finally {
      setDetailLoading(false);
    }
  };

  const handleSendReport = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedUser) return;
    setSendingReport(true);
    setReportMessage('');
    setDetailError('');
    try {
      const report = await sendUserReport(selectedUser.user.id, { title: reportTitle, body: reportBody });
      setSelectedUser(current => current ? { ...current, reports: [report, ...current.reports] } : current);
      setReportTitle('');
      setReportBody('');
      setReportMessage('Report sent to the user.');
    } catch (failure: unknown) {
      const detail = failure && typeof failure === 'object' && 'response' in failure
        ? (failure as { response?: { data?: { message?: string } } }).response?.data?.message
        : undefined;
      setDetailError(detail ?? 'Unable to send this report.');
    } finally {
      setSendingReport(false);
    }
  };

  const handleSendEmail = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedUser) return;
    setSendingEmail(true);
    setEmailMessage('');
    setEmailError('');
    try {
      const response = await sendUserEmail(selectedUser.user.id, { subject: emailSubject, body: emailBody });
      setEmailSubject('');
      setEmailBody('');
      setEmailMessage(response.message);
    } catch (failure: unknown) {
      const detail = failure && typeof failure === 'object' && 'response' in failure
        ? (failure as { response?: { data?: { message?: string } } }).response?.data?.message
        : undefined;
      setEmailError(detail ?? 'Unable to send this email. Check that the API is available and try again.');
    } finally {
      setSendingEmail(false);
    }
  };

  return (
    <div style={{ padding: 20, color: '#e2e8f0' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, gap: 12, flexWrap: 'wrap' }}>
        <div>
          <div style={{ color: '#38bdf8', textTransform: 'uppercase', letterSpacing: 2, fontSize: 12 }}>SmartGuard Admin</div>
          <h2 style={{ margin: '0.35rem 0 0', fontSize: 32 }}>Administration Dashboard</h2>
        </div>

        <button
          onClick={() => {
            clearStoredSession();
            navigate('/', { replace: true });
          }}
          style={{ background: 'rgba(15,23,42,0.85)', border: '1px solid rgba(148,163,184,0.18)', borderRadius: 12, color: '#f8fafc', padding: '0.8rem 1rem', cursor: 'pointer' }}
        >
          Logout
        </button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 16, marginBottom: 20 }}>
        {summary.map(item => (
          <div key={item.label} className="card" style={{ padding: 18 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
              <span style={{ color: '#94a3b8', fontSize: 13 }}>{item.label}</span>
              <div style={{ width: 32, height: 32, borderRadius: 10, display: 'flex', justifyContent: 'center', alignItems: 'center', background: '#38bdf8', color: '#020817' }}>{item.icon}</div>
            </div>
            <div style={{ fontSize: 28, fontWeight: 700 }}>{item.value}</div>
          </div>
        ))}
      </div>

      <section className="admin-property-panel">
        <div className="workspace-section-heading"><div><p className="eyebrow">Property portfolio</p><h2>All properties</h2></div><span className="count-label">{properties.length} properties</span></div>
        <div className="admin-property-grid">
          {properties.map(property => (
            <article className="admin-property-tile" key={property.id}>
              <div className="admin-property-image">
                {property.photoUrl
                  ? <img src={getPropertyPhotoUrl(property.photoUrl) ?? undefined} alt={`${property.name} property`} loading="lazy" />
                  : <Building2 size={26} aria-label="No property image" />}
              </div>
              <div className="admin-property-details"><strong>{property.name}</strong><span>{property.address}</span><small>{property.owner} · {property.status}</small></div>
            </article>
          ))}
          {!properties.length && <div className="empty-state">No properties are registered.</div>}
        </div>
      </section>

      {error && <p className="form-error" role="alert">{error}</p>}

      <div className="admin-live-grid">
        <section className="admin-live-panel">
          <div className="workspace-section-heading"><div><p className="eyebrow">IoT monitoring</p><h2>Connected devices</h2></div><span className="count-label">{devices.length} devices</span></div>
          {devices.map(device => <div className="device-row" key={device.deviceId}>
            <span className="device-signal" />
            <div><strong>{device.deviceId}</strong><span>{device.sensorType} · {device.propertyId}</span></div>
            <div><strong>{device.lastEventType}</strong><span>{new Date(device.lastSeen).toLocaleString()}</span></div>
          </div>)}
          {!devices.length && <div className="empty-state">No device events have been recorded.</div>}
        </section>
        <section className="admin-live-panel system-stat-panel">
          <div className="workspace-section-heading"><div><p className="eyebrow">System status</p><h2>Operations snapshot</h2></div><ShieldCheck size={18} color="#d5f477" /></div>
          <div className="system-stat-lines"><div><span>Open alerts</span><strong>{overview?.openAlerts ?? 0}</strong></div><div><span>Critical alerts</span><strong>{overview?.criticalAlertCount ?? 0}</strong></div><div><span>Risk score</span><strong>{overview?.riskScore ?? 0}<small> / 100</small></strong></div></div>
          <Link className="admin-action-link" to="/settings">Manage system settings <ArrowRightLeft size={15} /></Link>
        </section>
      </div>

      <div className="card" style={{ padding: 20, marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 18, gap: 12, flexWrap: 'wrap' }}>
          <h3 style={{ margin: 0 }}>User access overview</h3>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search users..."
              style={{ background: 'rgba(15,23,42,0.8)', border: '1px solid rgba(148,163,184,0.18)', color: '#f8fafc', borderRadius: 10, padding: '0.65rem 0.8rem', minWidth: 180 }}
            />
            <select
              value={filter}
              onChange={(e) => setFilter(e.target.value as 'All' | 'Blocked' | 'Active')}
              style={{ background: 'rgba(15,23,42,0.8)', border: '1px solid rgba(148,163,184,0.18)', color: '#f8fafc', borderRadius: 10, padding: '0.65rem 0.8rem' }}
            >
              <option value="All">All users</option>
              <option value="Active">Active</option>
              <option value="Blocked">Blocked</option>
            </select>
          </div>
        </div>

        <div style={{ height: 220 }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData}>
              <CartesianGrid stroke="#334155" vertical={false} />
              <XAxis dataKey="name" stroke="#94a3b8" />
              <YAxis stroke="#94a3b8" />
              <Tooltip />
              <Bar dataKey="value" fill="#38bdf8" radius={[8, 8, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 0.5fr', gap: 16 }}>
        <div className="card" style={{ padding: 20 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0 }}>User accounts</h3>
            <span style={{ color: '#38bdf8', fontSize: 12 }}>{filteredUsers.length} results</span>
          </div>

          <div style={{ display: 'grid', gap: 12 }}>
            {filteredUsers.map((user) => (
              <div key={user.id} style={{ padding: '0.9rem 1rem', background: 'rgba(15,23,42,0.8)', border: '1px solid rgba(148,163,184,0.12)', borderRadius: 14 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
                  <div>
                    <strong>{user.fullName}</strong>
                    <div style={{ color: '#94a3b8', fontSize: 12 }}>{user.email}</div>
                    <button className="user-profile-link" onClick={() => void openUserDetails(user)}>View profile and properties</button>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span style={{ background: user.isBlocked ? 'rgba(239,68,68,0.15)' : 'rgba(34,197,94,0.15)', color: user.isBlocked ? '#fca5a5' : '#86efac', border: `1px solid ${user.isBlocked ? 'rgba(248,113,113,0.4)' : 'rgba(34,197,94,0.4)'}`, borderRadius: 999, padding: '0.25rem 0.6rem', fontSize: 12 }}>
                      {user.isBlocked ? 'Blocked' : 'Active'}
                    </span>
                    <button
                      onClick={() => handleToggleBlock(user.id, !user.isBlocked)}
                      style={{ background: 'transparent', color: '#f8fafc', border: '1px solid rgba(148,163,184,0.18)', borderRadius: 10, padding: '0.45rem 0.7rem', cursor: 'pointer' }}
                    >
                      {user.isBlocked ? 'Unblock' : 'Block'}
                    </button>
                  </div>
                </div>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 10, color: '#cbd5e1', fontSize: 13, flexWrap: 'wrap' }}>
                  <span>{user.role}</span>
                  <span>Last login: {user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : 'Never'}</span>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="card" style={{ padding: 20 }}>
          <h3 style={{ marginTop: 0 }}>Admin privileges</h3>
          <div style={{ display: 'grid', gap: 12 }}>
            {[
              'Manage access policies',
              'Review all online users',
              'View security incidents',
              'Control alerts and lockdowns',
            ].map(item => (
              <div key={item} style={{ display: 'flex', alignItems: 'center', gap: 10, background: 'rgba(15,23,42,0.8)', border: '1px solid rgba(148,163,184,0.12)', borderRadius: 12, padding: '0.8rem 0.9rem' }}>
                <ArrowRightLeft size={16} color="#38bdf8" />
                <span>{item}</span>
              </div>
            ))}
          </div>

          <div style={{ marginTop: 18, padding: '1rem', background: 'linear-gradient(135deg, rgba(56,189,248,0.12), rgba(34,197,94,0.08))', borderRadius: 14 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
              <Activity size={16} color="#38bdf8" />
              <strong>Monitoring summary</strong>
            </div>
            <div style={{ color: '#cbd5e1' }}>All systems are operating normally across the monitored properties.</div>
          </div>
        </div>
      </div>

      {detailLoading && <div className="admin-modal-backdrop"><section className="admin-user-modal" role="status">Loading user profile…</section></div>}
      {detailError && !selectedUser && <p className="form-error" role="alert">{detailError}</p>}
      {selectedUser && <div className="admin-modal-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setSelectedUser(null); }}>
        <section className="admin-user-modal" role="dialog" aria-modal="true" aria-labelledby="admin-user-title">
          <header className="admin-user-modal-header">
            <div><p className="eyebrow">User profile</p><h2 id="admin-user-title">{selectedUser.user.fullName}</h2></div>
            <button className="icon-button" onClick={() => setSelectedUser(null)} aria-label="Close user details"><X size={18} /></button>
          </header>
          <div className="admin-user-detail-grid">
            <div className="admin-user-profile">
              <div className="admin-user-avatar">{selectedUser.user.profilePhotoUrl ? <a href={getProfilePhotoUrl(selectedUser.user.profilePhotoUrl) ?? undefined} target="_blank" rel="noreferrer" aria-label="Open uploaded profile photo"><img src={getProfilePhotoUrl(selectedUser.user.profilePhotoUrl) ?? undefined} alt={`${selectedUser.user.fullName} profile`} /></a> : selectedUser.user.fullName.slice(0, 1)}</div>
              <div className="admin-user-facts">
                <div><span>Account ID</span><strong>{selectedUser.user.id}</strong></div>
                <div><span>Email</span><strong>{selectedUser.user.email}</strong></div>
                <div><span>Role</span><strong>{selectedUser.user.role}</strong></div>
                <div><span>Account status</span><strong>{selectedUser.user.isBlocked ? 'Blocked' : 'Active'}</strong></div>
                <div><span>Joined</span><strong>{new Date(selectedUser.user.createdAt).toLocaleDateString()}</strong></div>
                <div><span>Last login</span><strong>{selectedUser.user.lastLoginAt ? new Date(selectedUser.user.lastLoginAt).toLocaleString() : 'Never'}</strong></div>
                <div><span>Last login IP</span><strong>{selectedUser.user.lastLoginIp ?? 'Not recorded'}</strong></div>
              </div>
            </div>
            <section className="admin-user-section">
              <div className="workspace-section-heading"><div><p className="eyebrow">Owned portfolio</p><h3>{selectedUser.properties.length} properties · uploaded images</h3></div></div>
              <div className="admin-user-property-grid">
                {selectedUser.properties.map(property => <article className="admin-user-property" key={property.id}>
                  <div className="admin-user-property-image">
                    {property.photoUrl
                      ? <a href={getPropertyPhotoUrl(property.photoUrl) ?? undefined} target="_blank" rel="noreferrer" aria-label={`Open uploaded image for ${property.name}`}><img src={getPropertyPhotoUrl(property.photoUrl) ?? undefined} alt={`${property.name} uploaded property image`} /></a>
                      : <Building2 size={22} />}
                  </div>
                  <div><strong>{property.name}</strong><span>{property.address}</span><small>Property ID: {property.id}</small><small>Owner: {property.owner} · Status: {property.status}</small><small>Added: {new Date(property.createdAt).toLocaleDateString()} · Updated: {new Date(property.updatedAt).toLocaleDateString()}</small></div>
                </article>)}
                {!selectedUser.properties.length && <div className="empty-state">No properties assigned to this user.</div>}
              </div>
            </section>
            <section className="admin-user-section admin-user-activity">
              <div className="workspace-section-heading"><div><p className="eyebrow">Security history</p><h3>{selectedUser.events.length} recent events · {selectedUser.alerts.length} alerts</h3></div></div>
              <div className="admin-user-event-list">{selectedUser.events.map(securityEvent => <article className="admin-user-event" key={securityEvent.id}><strong>{securityEvent.eventType} · {securityEvent.priority}</strong><span>{securityEvent.location} · {securityEvent.status} · {securityEvent.deviceId}</span><p>{securityEvent.description}</p><time>{new Date(securityEvent.timestamp).toLocaleString()}</time></article>)}{!selectedUser.events.length && <div className="empty-state">No security events for this user.</div>}</div>
              <div className="admin-user-alert-list"><h4>Alerts</h4>{selectedUser.alerts.map(alert => <div key={alert.id}><strong>{alert.priority} · {alert.status}</strong><span>{alert.message}</span><small>{new Date(alert.createdAt).toLocaleString()}</small></div>)}{!selectedUser.alerts.length && <div className="empty-state">No alerts for this user.</div>}</div>
            </section>
            <section className="admin-user-section admin-report-composer">
              <div className="workspace-section-heading"><div><p className="eyebrow">Direct report</p><h3>Send a report to {selectedUser.user.fullName.split(' ')[0]}</h3></div></div>
              <form onSubmit={handleSendReport} className="admin-report-form">
                <label>Subject<input value={reportTitle} onChange={event => setReportTitle(event.target.value)} maxLength={160} required placeholder="Property review" /></label>
                <label>Report<textarea value={reportBody} onChange={event => setReportBody(event.target.value)} maxLength={5000} required rows={4} placeholder="Write the report for this user…" /></label>
                {detailError && <p className="form-error" role="alert">{detailError}</p>}
                {reportMessage && <p className="form-success" role="status">{reportMessage}</p>}
                <button className="primary-button" type="submit" disabled={sendingReport}><Send size={14} />{sendingReport ? 'Sending…' : 'Send report'}</button>
              </form>
              {selectedUser.reports.length > 0 && <div className="admin-report-history"><strong>Previously sent</strong>{selectedUser.reports.map(report => <article key={report.id}><div><span>{report.title}</span><time>{new Date(report.createdAt).toLocaleString()}</time></div><p>{report.body}</p><small>{report.isRead ? `Read ${report.readAt ? new Date(report.readAt).toLocaleString() : ''}` : 'Unread'}</small></article>)}</div>}
            </section>
            <section className="admin-user-section admin-email-composer">
              <div className="workspace-section-heading"><div><p className="eyebrow">Email message</p><h3>Send an email to {selectedUser.user.fullName.split(' ')[0]}</h3><p className="admin-email-recipient">To: {selectedUser.user.email}</p></div></div>
              <form onSubmit={handleSendEmail} className="admin-report-form">
                <label>Subject<input value={emailSubject} onChange={event => setEmailSubject(event.target.value)} maxLength={160} required placeholder="SmartGuard account update" /></label>
                <label>Message<textarea value={emailBody} onChange={event => setEmailBody(event.target.value)} maxLength={5000} required rows={4} placeholder="Write an email to this user…" /></label>
                {emailError && <p className="form-error" role="alert">{emailError}</p>}
                {emailMessage && <p className="form-success" role="status">{emailMessage}</p>}
                <button className="primary-button" type="submit" disabled={sendingEmail}><Mail size={14} />{sendingEmail ? 'Sending email…' : 'Send email'}</button>
              </form>
            </section>
          </div>
        </section>
      </div>}
    </div>
  );
}
