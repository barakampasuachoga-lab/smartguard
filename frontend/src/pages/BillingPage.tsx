import { useEffect, useState } from 'react';
import { Download, ReceiptText } from 'lucide-react';
import { getMySubscription } from '../api';
import type { SubscriptionSnapshot } from '../types';

const money = (value: number) => `KSh ${value.toLocaleString('en-KE')}`;
const dateLabel = (value: string) => new Date(value).toLocaleString('en-KE', { dateStyle: 'medium', timeStyle: 'short' });

function downloadInvoice(snapshot: SubscriptionSnapshot, invoiceId: string) {
  const invoice = snapshot.invoices.find(item => item.id === invoiceId);
  const details = [
    'SMARTGUARD PAYMENT RECEIPT',
    `Invoice: ${invoice?.invoiceNumber ?? '—'}`,
    `Plan: ${snapshot.plan.name}`,
    `Amount: ${money(invoice?.amountKes ?? 0)} ${invoice?.currency ?? 'KES'}`,
    `Issued: ${invoice ? dateLabel(invoice.issuedAt) : '—'}`,
    `M-PESA receipt: ${invoice?.mpesaReceiptNumber ?? '—'}`,
    '',
    'Thank you for choosing SmartGuard.'
  ].join('\r\n');
  const url = URL.createObjectURL(new Blob([details], { type: 'text/plain;charset=utf-8' }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `${invoice?.invoiceNumber ?? 'smartguard-receipt'}.txt`;
  anchor.click();
  URL.revokeObjectURL(url);
}

export default function BillingPage() {
  const [snapshot, setSnapshot] = useState<SubscriptionSnapshot | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    getMySubscription().then(setSnapshot).catch(() => setError('Could not load billing history. Please refresh the page.'));
  }, []);

  return <div style={{ padding: 20, color: '#e2e8f0', maxWidth: 1180, margin: '0 auto' }}>
    <div style={{ marginBottom: 22 }}>
      <div style={{ color: '#38bdf8', textTransform: 'uppercase', letterSpacing: 2, fontSize: 12 }}>SmartGuard Account</div>
      <h2 style={{ margin: '0.35rem 0 0', fontSize: 32 }}>Billing</h2>
      <p style={{ color: '#94a3b8', marginBottom: 0 }}>Payment attempts, M-PESA confirmations and invoices.</p>
    </div>
    {error ? <div role="alert" style={{ color: '#fecaca', background: 'rgba(127,29,29,.35)', padding: 14, borderRadius: 12 }}>{error}</div> : null}
    {!snapshot ? !error ? <div style={{ color: '#cbd5e1' }}>Loading billing history…</div> : null : <>
      <section className="card" style={{ padding: 20, marginBottom: 18 }}>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}><ReceiptText size={18} color="#38bdf8" /><h3 style={{ margin: 0 }}>Payment history</h3></div>
        {snapshot.paymentTransactions.length === 0 ? <p style={{ color: '#94a3b8' }}>No payment attempts yet.</p> : <div style={{ overflowX: 'auto', marginTop: 12 }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 700 }}>
            <thead><tr>{['Date', 'Plan', 'Amount', 'Phone', 'Status', 'M-PESA receipt'].map(label => <th key={label} style={{ textAlign: 'left', padding: 10, color: '#94a3b8', borderBottom: '1px solid rgba(148,163,184,.2)' }}>{label}</th>)}</tr></thead>
            <tbody>{snapshot.paymentTransactions.map(payment => <tr key={payment.id}>
              <td style={{ padding: 10, borderBottom: '1px solid rgba(148,163,184,.1)' }}>{dateLabel(payment.initiatedAt)}</td>
              <td style={{ padding: 10, borderBottom: '1px solid rgba(148,163,184,.1)' }}>{payment.planCode}</td>
              <td style={{ padding: 10, borderBottom: '1px solid rgba(148,163,184,.1)' }}>{money(payment.amountKes)}</td>
              <td style={{ padding: 10, borderBottom: '1px solid rgba(148,163,184,.1)' }}>{payment.phoneNumber}</td>
              <td style={{ padding: 10, borderBottom: '1px solid rgba(148,163,184,.1)' }}>{payment.status}</td>
              <td style={{ padding: 10, borderBottom: '1px solid rgba(148,163,184,.1)' }}>{payment.mpesaReceiptNumber ?? '—'}</td>
            </tr>)}</tbody>
          </table>
        </div>}
      </section>

      <section className="card" style={{ padding: 20 }}>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}><ReceiptText size={18} color="#38bdf8" /><h3 style={{ margin: 0 }}>Invoices and receipts</h3></div>
        {snapshot.invoices.length === 0 ? <p style={{ color: '#94a3b8' }}>Receipts appear here after a payment is confirmed.</p> : <div style={{ display: 'grid', gap: 10, marginTop: 12 }}>
          {snapshot.invoices.map(invoice => <div key={invoice.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', padding: 12, borderRadius: 10, background: 'rgba(15,23,42,.55)' }}>
            <div><strong>{invoice.invoiceNumber}</strong><div style={{ color: '#94a3b8', fontSize: 13 }}>{dateLabel(invoice.issuedAt)} · {money(invoice.amountKes)} {invoice.currency}</div></div>
            <button className="button button-outline button-small" onClick={() => downloadInvoice(snapshot, invoice.id)}><Download size={15} /> Download receipt</button>
          </div>)}
        </div>}
      </section>
    </>}
  </div>;
}
