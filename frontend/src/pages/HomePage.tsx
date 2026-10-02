import { Activity, ArrowDownRight, ArrowRight, BellRing, Camera, ChartNoAxesCombined, CircleDot, Fingerprint, ShieldCheck } from 'lucide-react';
import { Link } from 'react-router-dom';

const featureCards = [
  {
    icon: ShieldCheck,
    index: '01',
    title: 'Smart Monitoring',
    description: 'See the security status of every property in one live, easy-to-scan view.',
  },
  {
    icon: Camera,
    index: '02',
    title: 'IoT Integration',
    description: 'Bring cameras, access control, and sensor activity into a shared timeline.',
  },
  {
    icon: Fingerprint,
    index: '03',
    title: 'Anomaly Detection',
    description: 'Surface unusual patterns early with rules that help teams focus attention.',
  },
  {
    icon: BellRing,
    index: '04',
    title: 'Real-Time Alerts',
    description: 'Prioritize incoming warnings and make review status visible to the team.',
  },
  {
    icon: Activity,
    index: '05',
    title: 'Incident Management',
    description: 'Review security events with the property, device, location, and context attached.',
  },
  {
    icon: ChartNoAxesCombined,
    index: '06',
    title: 'Security Analytics',
    description: 'Turn activity across your properties into useful operational reporting.',
  },
];

const steps = [
  { number: '01', title: 'Connect your properties', text: 'Create a secure workspace for each property and the people responsible for it.' },
  { number: '02', title: 'Bring activity together', text: 'Review device events, anomaly signals, and priority alerts in one place.' },
  { number: '03', title: 'Respond with context', text: 'Track incidents, review outcomes, and export reports when your team needs them.' },
];

export default function HomePage() {
  return (
    <div className="public-site">
      <section className="landing-hero" id="home">
        <img className="hero-photo" src="https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=2200&q=88" alt="Modern residence at dusk with warm exterior lighting" />
        <div className="hero-shade" />
        <header className="public-header">
          <Link className="public-brand" to="/" aria-label="SmartGuard home">
            <span className="brand-mark"><ShieldCheck size={20} /></span>
            <span>SMARTGUARD</span>
          </Link>
          <nav className="public-nav" aria-label="Main navigation">
            <a href="#home">Home</a>
            <a href="#features">Features</a>
            <a href="#how-it-works">How It Works</a>
            <a href="#about">About</a>
            <a href="#contact">Contact</a>
          </nav>
          <div className="public-actions">
            <Link className="login-link" to="/login">Login</Link>
            <Link className="button button-lime button-small" to="/register">Get Started <ArrowRight size={16} /></Link>
          </div>
        </header>
        <main className="hero-content">
          <div className="hero-kicker"><span /> PROPERTY SECURITY, IN ONE PLACE</div>
          <h1>Protect Your Property.<br /><em>Monitor Every Event.</em></h1>
          <p>SmartGuard brings property management, IoT security events, anomaly detection, alerts, and security analytics together in one centralized platform.</p>
          <div className="hero-actions">
            <Link className="button button-lime" to="/register">Get Started <ArrowRight size={17} /></Link>
            <a className="button button-outline" href="#how-it-works">Learn More <ArrowDownRight size={17} /></a>
          </div>
        </main>
        <div className="hero-caption"><span className="live-indicator" /> A clearer view of what matters.</div>
      </section>

      <main>
        <section className="features-section section-wrap" id="features">
          <div className="section-heading">
            <div><p className="section-kicker">ONE CONNECTED PLATFORM</p><h2>Security, with the full picture.</h2></div>
            <p>From the first sensor signal to the final report, keep your property operations connected and clear.</p>
          </div>
          <div className="feature-grid">
            {featureCards.map(({ icon: Icon, index, title, description }) => (
              <article className="feature-item" key={title}>
                <div className="feature-topline"><span>{index} / 06</span><Icon size={21} strokeWidth={1.8} /></div>
                <h3>{title}</h3><p>{description}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="how-section" id="how-it-works">
          <div className="section-wrap how-inner">
            <div className="how-intro"><p className="section-kicker">HOW IT WORKS</p><h2>One view.<br />A more confident response.</h2><p>Make it easier for owners and administrators to stay in sync as activity unfolds.</p></div>
            <div className="steps-list">
              {steps.map(step => <article className="step-row" key={step.number}><span>{step.number}</span><div><h3>{step.title}</h3><p>{step.text}</p></div><CircleDot size={18} /></article>)}
            </div>
          </div>
        </section>

        <section className="about-section section-wrap" id="about">
          <div className="about-image-wrap"><img src="https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?auto=format&fit=crop&w=1200&q=85" alt="Bright, secure residential interior" loading="lazy" /><span>DESIGNED AROUND REAL OPERATIONS</span></div>
          <div className="about-copy"><p className="section-kicker">ABOUT SMARTGUARD</p><h2>Better visibility.<br />Less guesswork.</h2><p>SmartGuard gives property owners and security administrators a shared place to manage properties, follow incidents, review alerts, and understand activity over time.</p><p>Role-based access keeps each person focused on the information and actions that belong to them.</p><Link className="text-link" to="/register">Create your workspace <ArrowRight size={16} /></Link></div>
        </section>

        <section className="contact-section" id="contact">
          <div className="section-wrap contact-inner"><div><p className="section-kicker">READY WHEN YOU ARE</p><h2>Bring your security picture together.</h2></div><Link className="button button-lime" to="/register">Get Started <ArrowRight size={17} /></Link></div>
        </section>
      </main>
      <footer className="public-footer section-wrap"><Link className="public-brand" to="/"><span className="brand-mark"><ShieldCheck size={18} /></span><span>SMARTGUARD</span></Link><span>Property security, made clearer.</span><a href="mailto:hello@smartguard.local">Contact the team</a><span>© 2026 SmartGuard</span></footer>
    </div>
  );
}
