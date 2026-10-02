import { Download } from 'lucide-react';
import { downloadSecurityReport, downloadUserReport } from '../api';
import { useEffect, useState } from 'react';
import { getMyReports } from '../api';
import type { UserReport } from '../types';

export default function ReportsPage() {
  const [reports, setReports] = useState<UserReport[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    void getMyReports().then(setReports).catch(() => setError('Unable to load reports sent to you.'));
  }, []);

  const handleDownload = async () => {
    await downloadSecurityReport();
  };

  return (
    <div style={{ display: 'grid', gap: 20 }}>
      <section className="workspace-section">
        <div className="workspace-section-heading"><div><p className="eyebrow">Administrator messages</p><h2>Reports sent to you</h2></div><span className="count-label">{reports.length} reports</span></div>
        {error && <p className="form-error" role="alert">{error}</p>}
        <div className="user-report-list">
          {reports.map(report => <article className="user-report-item" key={report.id}>
            <div className="user-report-heading"><strong>{report.title}</strong><time>{new Date(report.createdAt).toLocaleString()}</time></div>
            <p>{report.body}</p>
            <div className="user-report-footer">
              <span>From SmartGuard administration</span>
              <button className="subtle-button" type="button" onClick={() => downloadUserReport(report)}>
                <Download size={13} /> Download report
              </button>
            </div>
          </article>)}
          {!reports.length && !error && <div className="empty-state">Reports sent by an administrator will appear here.</div>}
        </div>
      </section>
      <div className="card" style={{ padding: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <h3 style={{ margin: 0 }}>Reports</h3>
          <button
            onClick={handleDownload}
            style={{ background: 'linear-gradient(135deg, #38bdf8, #22c55e)', color: '#03131e', border: 'none', borderRadius: 10, padding: '0.8rem 1rem', fontWeight: 700, cursor: 'pointer' }}
          >
            Download CSV report
          </button>
        </div>
      </div>

      <div className="card" style={{ padding: 20 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16 }}>
          {[
            { title: 'Weekly summary', value: '94%', subtitle: 'overall response coverage' },
            { title: 'False alarm rate', value: '6.2%', subtitle: 'down from 8.4%' },
            { title: 'Response time', value: '4.2 min', subtitle: 'median on critical events' },
            { title: 'System uptime', value: '99.97%', subtitle: 'last 30 days' },
          ].map(item => (
            <div key={item.title} style={{ padding: '1.2rem', borderRadius: 14, background: 'rgba(15,23,42,0.8)', border: '1px solid rgba(148,163,184,0.12)' }}>
              <div style={{ color: '#94a3b8', fontSize: 13 }}>{item.title}</div>
              <div style={{ fontSize: 30, fontWeight: 700, margin: '8px 0' }}>{item.value}</div>
              <div style={{ color: '#94a3b8', fontSize: 12 }}>{item.subtitle}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
