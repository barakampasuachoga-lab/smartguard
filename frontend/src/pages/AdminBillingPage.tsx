import { useEffect, useState } from 'react';
import { getAdminBillingPayments, getAdminBillingSummary } from '../api';
import type { AdminBillingSummary } from '../api';

const money = (value: number) => `KSh ${value.toLocaleString('en-KE')}`;
const dateLabel = (value?: string | null) => value ? new Date(value).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—';

export default function AdminBillingPage() {
  const [summary, setSummary] = useState<AdminBillingSummary | null>(null);
  const [payments, setPayments] = useState<Awaited<ReturnType<typeof getAdminBillingPayments>>>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    Promise.all([getAdminBillingSummary(), getAdminBillingPayments()])
      .then(([billing, transactions]) => { setSummary(billing); setPayments(transactions); })
      .catch(() => setError('Could not load billing metrics. Ensure you are signed in as an administrator.'));
  }, []);

  return <div style={{ padding: 20, color: '#e2e8f0', maxWidth: 1240, margin: '0 auto' }}>
    <div style={{ marginBottom: 22 }}>
      <div style={{ color: '#38bdf8', textTransform: 'uppercase', letterSpacing: 2, fontSize: 12 }}>SmartGuard Admin</div>
      <h2 style={{ margin: '0.35rem 0 0', fontSize: 32 }}>Billing & subscriptions</h2>
      <p style={{ color: '#94a3b8', marginBottom: 0 }}>Subscription status and confirmed M-PESA revenue.</p>
    </div>
    {error ? <div role="alert" style={{ color: '#fecaca', background: 'rgba(127,29,29,.35)', padding: 14, borderRadius: 12 }}>{error}</div> : null}
    {!summary ? !error ? <div style={{ color: '#cbd5e1' }}>Loading subscription records…</div> : null : <>
      <section style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(185px,1fr))', gap: 14, marginBottom: 20 }}>
        {[
          ['Revenue this month', money(summary.revenueThisMonthKes)],
          ['Total collected', money(summary.totalRevenueKes)],
          ['Active subscriptions', summary.activeSubscriptions],
          ['Free trials', summary.trialSubscriptions],
          ['Past due', summary.pastDueSubscriptions],
        ].map(([label, value]) => <div className="card" key={label} style={{ padding: 18 }}><div style={{ color: '#94a3b8', fontSize: 13 }}>{label}</div><strong style={{ display: 'block', fontSize: 24, marginTop: 8 }}>{value}</strong></div>)}
      </section>
      <section className="card" style={{ padding: 20, marginBottom: 18 }}>
        <h3 style={{ marginTop: 0 }}>Subscriptions</h3>
        {summary.subscriptions.length === 0 ? <p style={{ color: '#94a3b8' }}>No subscription records yet.</p> : <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 720 }}>
            <thead><tr>{['Customer', 'Email', 'Plan', 'Status', 'Trial ends', 'Period ends', 'Cancellation'].map(label => <th key={label} style={{ textAlign: 'left', padding: 10, color: '#94a3b8', borderBottom: '1px solid rgba(148,163,184,.2)' }}>{label}</th>)}</tr></thead>
            <tbody>{summary.subscriptions.map(subscription => <tr key={subscription.id}>
              <td style={{ padding: 10 }}>{subscription.userName ?? `User ${subscription.userId}`}</td><td style={{ padding: 10 }}>{subscription.email ?? '—'}</td><td style={{ padding: 10 }}>{subscription.plan ?? '—'}</td><td style={{ padding: 10 }}>{subscription.status}</td><td style={{ padding: 10 }}>{dateLabel(subscription.trialEnd)}</td><td style={{ padding: 10 }}>{dateLabel(subscription.currentPeriodEnd)}</td><td style={{ padding: 10 }}>{subscription.cancelAtPeriodEnd ? 'Scheduled' : '—'}</td>
            </tr>)}</tbody>
          </table>
        </div>}
      </section>
      <section className="card" style={{ padding: 20 }}>
        <h3 style={{ marginTop: 0 }}>Confirmed M-PESA payments</h3>
        {payments.length === 0 ? <p style={{ color: '#94a3b8' }}>No confirmed payments yet.</p> : <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 620 }}>
            <thead><tr>{['Date', 'Customer', 'Amount', 'M-PESA receipt'].map(label => <th key={label} style={{ textAlign: 'left', padding: 10, color: '#94a3b8', borderBottom: '1px solid rgba(148,163,184,.2)' }}>{label}</th>)}</tr></thead>
            <tbody>{payments.map(row => <tr key={row.payment.id}><td style={{ padding: 10 }}>{dateLabel(row.payment.paidAt)}</td><td style={{ padding: 10 }}>{row.user?.fullName ?? `User ${row.payment.userId}`}</td><td style={{ padding: 10 }}>{money(row.payment.amountKes)}</td><td style={{ padding: 10 }}>{row.payment.mpesaReceiptNumber}</td></tr>)}</tbody>
          </table>
        </div>}
      </section>
    </>}
  </div>;
}
