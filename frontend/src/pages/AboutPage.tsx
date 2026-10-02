import { ArrowRight, ShieldCheck, Sparkles, Target, Users } from 'lucide-react';
import { Link } from 'react-router-dom';

const navItems = [
  { label: 'Home', to: '/' },
  { label: 'About Us', to: '/about' },
  { label: 'Login', to: '/login' },
];

const values = [
  {
    icon: Target,
    title: 'Focused on safety',
    description: 'We design tools that help teams act early and keep properties protected around the clock.',
  },
  {
    icon: Users,
    title: 'Built for teams',
    description: 'Clear workflows and role-based access keep the right people informed and in control.',
  },
  {
    icon: Sparkles,
    title: 'Always improving',
    description: 'Our platform evolves with new insights and operational intelligence for more resilient security.',
  },
];

export default function AboutPage() {
  return (
    <div style={{ minHeight: '100vh', background: '#020817', color: '#e2e8f0' }}>
      <nav style={{ maxWidth: 1200, margin: '0 auto', padding: '1.25rem 1.5rem', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
        <Link to="/" style={{ display: 'flex', alignItems: 'center', gap: 12, color: '#f8fafc', fontWeight: 700, fontSize: 22 }}>
          <span style={{ width: 38, height: 38, borderRadius: 12, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'linear-gradient(135deg, #22c55e, #38bdf8)', color: '#071827' }}>
            <ShieldCheck size={20} />
          </span>
          SmartGuard
        </Link>

        <div style={{ display: 'flex', alignItems: 'center', gap: 24, color: '#cbd5e1' }}>
          {navItems.map((item) => (
            <Link key={item.to} to={item.to} style={{ padding: '0.5rem 0.8rem', borderRadius: 10, fontWeight: 600 }}>
              {item.label}
            </Link>
          ))}
        </div>

        <Link to="/login" style={{ display: 'inline-flex', alignItems: 'center', gap: 8, padding: '0.8rem 1.2rem', borderRadius: 12, background: 'linear-gradient(135deg, #38bdf8, #22c55e)', color: '#03131e', fontWeight: 700 }}>
          Login
          <ArrowRight size={16} />
        </Link>
      </nav>

      <main style={{ maxWidth: 1100, margin: '0 auto', padding: '2rem 1.5rem 4rem' }}>
        <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 28, alignItems: 'center', padding: '2rem 0 2.5rem' }}>
          <div>
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 8, padding: '0.4rem 0.8rem', borderRadius: 999, background: 'rgba(56, 189, 248, 0.12)', border: '1px solid rgba(56, 189, 248, 0.25)', color: '#7dd3fc', fontSize: 12, letterSpacing: '0.12em', textTransform: 'uppercase' }}>
              About SmartGuard
            </div>
            <h1 style={{ margin: '1rem 0 1rem', fontSize: 'clamp(2.4rem, 4vw, 4rem)', lineHeight: 1.1, letterSpacing: '-0.05em' }}>
              Security insight that helps teams act with confidence.
            </h1>
            <p style={{ margin: 0, fontSize: 18, color: '#cbd5e1', lineHeight: 1.7 }}>
              SmartGuard brings together live security events, activity tracking, and a clear command view so teams can spot patterns, reduce response time, and support safer buildings and communities.
            </p>
          </div>

          <div style={{ background: 'rgba(15, 23, 42, 0.9)', border: '1px solid rgba(148, 163, 184, 0.2)', borderRadius: 24, padding: '1.5rem' }}>
            <div style={{ display: 'grid', gap: 14 }}>
              <div style={{ padding: '1rem 1.1rem', borderRadius: 16, background: 'rgba(2, 6, 23, 0.8)', border: '1px solid rgba(148, 163, 184, 0.15)' }}>
                <strong style={{ display: 'block', fontSize: 18, marginBottom: 6 }}>Mission</strong>
                <span style={{ color: '#cbd5e1' }}>To make security operations clearer, faster, and more proactive.</span>
              </div>
              <div style={{ padding: '1rem 1.1rem', borderRadius: 16, background: 'rgba(2, 6, 23, 0.8)', border: '1px solid rgba(148, 163, 184, 0.15)' }}>
                <strong style={{ display: 'block', fontSize: 18, marginBottom: 6 }}>Approach</strong>
                <span style={{ color: '#cbd5e1' }}> combine real-time alerts with human-friendly dashboards and role-aware access.</span>
              </div>
              <div style={{ padding: '1rem 1.1rem', borderRadius: 16, background: 'rgba(2, 6, 23, 0.8)', border: '1px solid rgba(148, 163, 184, 0.15)' }}>
                <strong style={{ display: 'block', fontSize: 18, marginBottom: 6 }}>Result</strong>
                <span style={{ color: '#cbd5e1' }}>Better visibility, quicker response times, and stronger confidence in daily operations.</span>
              </div>
            </div>
          </div>
        </section>

        <section style={{ paddingTop: '1rem' }}>
          <h2 style={{ textAlign: 'center', marginBottom: 28, fontSize: 32 }}>What guides us</h2>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 20 }}>
            {values.map(({ icon: Icon, title, description }) => (
              <div key={title} style={{ background: 'rgba(15, 23, 42, 0.9)', border: '1px solid rgba(148, 163, 184, 0.2)', borderRadius: 22, padding: '1.5rem' }}>
                <div style={{ width: 52, height: 52, borderRadius: 14, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'linear-gradient(135deg, rgba(56, 189, 248, 0.18), rgba(34, 197, 94, 0.18))', color: '#7dd3fc', marginBottom: 16 }}>
                  <Icon size={24} />
                </div>
                <h3 style={{ margin: '0 0 0.7rem', fontSize: 22 }}>{title}</h3>
                <p style={{ margin: 0, color: '#cbd5e1', lineHeight: 1.7 }}>{description}</p>
              </div>
            ))}
          </div>
        </section>
      </main>
    </div>
  );
}
