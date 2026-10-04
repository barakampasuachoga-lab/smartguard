import { useEffect, useMemo, useState } from 'react';
import { ArrowUpRight, Check, Clock3, Smartphone } from 'lucide-react';
import { cancelSubscription, getMySubscription, getPaymentTransaction, getSubscriptionPlans, startSubscriptionCheckout } from '../api';
import type { SubscriptionPlan, SubscriptionSnapshot } from '../types';

const dateLabel = (value?: string | null) => value ? new Date(value).toLocaleDateString('en-KE', { day: '2-digit', month: 'long', year: 'numeric' }) : '—';
const money = (value: number) => `KSh ${value.toLocaleString('en-KE')}`;

function features(plan: SubscriptionPlan) {
  return [
    [`${plan.maxProperties} ${plan.maxProperties === 1 ? 'property' : 'properties'}`, true],
    [`${plan.maxDevices} devices`, true],
    ['Basic alerts', true],
    ['Anomaly detection', plan.anomalyDetection],
    ['Analytics', plan.analytics],
    ['Incident management', plan.incidentManagement],
    ['Security Intelligence', plan.securityIntelligence],
    ['Multiple staff accounts', plan.multipleStaffAccounts],
    ['Advanced reports', plan.advancedReports],
    ['Priority support', plan.prioritySupport],
  ] as const;
}

export default function SubscriptionPage() {
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [snapshot, setSnapshot] = useState<SubscriptionSnapshot | null>(null);
  const [selectedPlan, setSelectedPlan] = useState('STANDARD');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const load = async () => {
    const [availablePlans, current] = await Promise.all([getSubscriptionPlans(), getMySubscription()]);
    setPlans(availablePlans);
    setSnapshot(current);
    setSelectedPlan(current.plan.code);
  };

  useEffect(() => {
    load().catch(() => setError('Could not load subscription details. Please refresh the page.')).finally(() => setIsLoading(false));
  }, []);

  const chosenPlan = useMemo(() => plans.find(plan => plan.code === selectedPlan), [plans, selectedPlan]);
  const trialDays = snapshot?.subscription.trialEnd
    ? Math.max(0, Math.ceil((new Date(snapshot.subscription.trialEnd).getTime() - Date.now()) / 86400000))
    : 0;
  const nextPaymentDate = snapshot?.subscription.currentPeriodEnd ?? snapshot?.subscription.trialEnd;

  const beginCheckout = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!chosenPlan) return;
    setIsSubmitting(true);
    setError('');
    setNotice('Sending an M-PESA prompt…');
    try {
      const checkout = await startSubscriptionCheckout(chosenPlan.code, phoneNumber);
      setNotice(`${checkout.message} Waiting for M-PESA confirmation…`);
      for (let attempt = 0; attempt < 40; attempt += 1) {
        await new Promise(resolve => window.setTimeout(resolve, 3000));
        const payment = await getPaymentTransaction(checkout.id);
        if (payment.status === 'SUCCEEDED') {
          await load();
          setNotice(`Payment received. ${chosenPlan.name} is active. The next renewal date is shown above.`);
          return;
        }
        if (payment.status === 'FAILED') {
          setError(payment.responseDescription ?? 'M-PESA did not complete the payment. You can try again.');
          setNotice('');
          return;
        }
      }
      setNotice('The payment is still pending. This page will show the updated status after M-PESA sends its confirmation.');
      await load();
    } catch (failure: unknown) {
      const message = failure && typeof failure === 'object' && 'response' in failure
        ? (failure as { response?: { data?: { message?: string } } }).response?.data?.message
        : undefined;
      setError(message ?? 'Could not start the M-PESA payment. Check the phone number and try again.');
      setNotice('');
    } finally {
      setIsSubmitting(false);
    }
  };

  const cancel = async () => {
    if (!window.confirm('Schedule this subscription to end when the current access period finishes?')) return;
    setError('');
    try {
      const result = await cancelSubscription();
      setSnapshot(current => current ? { ...current, subscription: result.subscription } : current);
      setNotice(result.message);
    } catch {
      setError('Could not schedule cancellation. Please try again.');
    }
  };

  if (isLoading) return <div style={{ padding: 24, color: '#cbd5e1' }}>Loading subscription…</div>;

  return (
    <div style={{ padding: 20, color: '#e2e8f0', maxWidth: 1180, margin: '0 auto' }}>
      <div style={{ marginBottom: 22 }}>
        <div style={{ color: '#38bdf8', textTransform: 'uppercase', letterSpacing: 2, fontSize: 12 }}>SmartGuard Account</div>
        <h2 style={{ margin: '0.35rem 0 0', fontSize: 32 }}>Subscription</h2>
        <p style={{ color: '#94a3b8', marginBottom: 0 }}>Choose the monitoring plan that fits your properties and devices.</p>
      </div>

      {snapshot ? <section className="card" style={{ padding: 22, marginBottom: 22, display: 'grid', gridTemplateColumns: '1fr auto', gap: 20, alignItems: 'center' }}>
        <div>
          <div style={{ color: '#94a3b8', fontSize: 12, letterSpacing: 1.3 }}>YOUR CURRENT PLAN</div>
          <h3 style={{ margin: '8px 0', fontSize: 25 }}>{snapshot.plan.name.toUpperCase()}</h3>
          <div style={{ color: '#cbd5e1' }}>{money(snapshot.plan.priceKes)} / month</div>
          <div style={{ marginTop: 10, color: snapshot.subscription.status === 'TRIAL' ? '#7dd3fc' : '#94a3b8' }}>
            {snapshot.subscription.status === 'TRIAL'
              ? `${trialDays} days remaining in your 14-day Standard trial · ends ${dateLabel(snapshot.subscription.trialEnd)}`
              : snapshot.subscription.status === 'PENDING_APPROVAL'
                ? 'Payment confirmed · waiting for administrator approval before monitoring is activated'
              : snapshot.subscription.status === 'ACTIVE'
                ? `Next payment due: ${dateLabel(snapshot.subscription.currentPeriodEnd)} · renew by sending another M-PESA payment`
                : `Status: ${snapshot.subscription.status} · choose a plan to activate monitoring`}
          </div>
          {snapshot.subscription.cancelAtPeriodEnd ? <div style={{ marginTop: 6, color: '#fbbf24' }}>Cancellation scheduled for {dateLabel(snapshot.subscription.currentPeriodEnd ?? snapshot.subscription.trialEnd)}.</div> : null}
        </div>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          <a href="#plans" className="button button-lime" style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>Manage subscription <ArrowUpRight size={16} /></a>
          {['ACTIVE', 'TRIAL'].includes(snapshot.subscription.status) && !snapshot.subscription.cancelAtPeriodEnd ? <button className="button button-outline" onClick={cancel}>Cancel at period end</button> : null}
        </div>
      </section> : null}

      {error ? <div role="alert" style={{ color: '#fecaca', background: 'rgba(127,29,29,.35)', padding: 14, borderRadius: 12, marginBottom: 16 }}>{error}</div> : null}
      {notice ? <div role="status" style={{ color: '#bbf7d0', background: 'rgba(20,83,45,.3)', padding: 14, borderRadius: 12, marginBottom: 16 }}>{notice}</div> : null}

      <section id="plans" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 16 }}>
        {plans.map(plan => {
          const current = plan.code === snapshot?.subscription.planCode;
          const selected = plan.code === selectedPlan;
          return <div key={plan.code} role="button" aria-pressed={selected} tabIndex={0} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') setSelectedPlan(plan.code); }} onClick={() => setSelectedPlan(plan.code)} className="card" style={{ textAlign: 'left', color: '#e2e8f0', padding: 20, border: selected ? '1px solid #38bdf8' : undefined, cursor: 'pointer' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ margin: 0 }}>{plan.name}</h3>
              {current ? <span style={{ color: '#86efac', fontSize: 12 }}>CURRENT</span> : null}
            </div>
            <div style={{ fontSize: 25, fontWeight: 800, margin: '14px 0 4px' }}>{money(plan.priceKes)}<span style={{ color: '#94a3b8', fontSize: 13, fontWeight: 400 }}> / month</span></div>
            <ul style={{ listStyle: 'none', display: 'grid', gap: 8, padding: 0, margin: '18px 0 0', color: '#cbd5e1', fontSize: 13 }}>
              {features(plan).map(([label, included]) => <li key={label} style={{ display: 'flex', gap: 8, color: included ? '#cbd5e1' : '#64748b' }}>
                <span style={{ color: included ? '#4ade80' : '#64748b' }}>{included ? <Check size={15} /> : '×'}</span>{label}
              </li>)}
            </ul>
          </div>;
        })}
      </section>

      {chosenPlan ? <section className="card" style={{ padding: 22, marginTop: 22, maxWidth: 560 }}>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', color: '#38bdf8' }}><Smartphone size={19} /><h3 style={{ margin: 0, color: '#e2e8f0' }}>Pay with Lipa na M-PESA</h3></div>
        <p style={{ color: '#94a3b8', lineHeight: 1.55 }}>You’ll receive an STK Push on your phone. Confirm it using your M-PESA PIN. After Safaricom confirms payment, an administrator reviews and activates the subscription.</p>
        <form onSubmit={beginCheckout} style={{ display: 'grid', gap: 14 }}>
          <label style={{ display: 'grid', gap: 8, fontSize: 13, color: '#cbd5e1' }}>M-PESA phone number
            <input required autoComplete="tel" inputMode="tel" placeholder="0712345678" value={phoneNumber} onChange={event => setPhoneNumber(event.target.value)} style={{ borderRadius: 10, border: '1px solid rgba(148,163,184,.25)', padding: 12, color: '#f8fafc', background: 'rgba(15,23,42,.8)' }} />
          </label>
          <button type="submit" disabled={isSubmitting} className="button button-lime" style={{ display: 'flex', gap: 8, alignItems: 'center', justifyContent: 'center' }}><Clock3 size={16} />{isSubmitting ? 'Waiting for payment…' : `Pay ${money(chosenPlan.priceKes)} with M-PESA`}</button>
        </form>
        <small style={{ display: 'block', color: '#64748b', marginTop: 12 }}>Monthly renewals require a new STK Push and your confirmation. SmartGuard does not store your M-PESA PIN.</small>
      </section> : null}
    </div>
  );
}
