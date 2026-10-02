import type { ReactNode } from 'react';

interface StatCardProps {
  title: string;
  value: string | number;
  subtitle: string;
  trend?: string;
  accent?: string;
  icon?: ReactNode;
}

export default function StatCard({ title, value, subtitle, trend, accent = '#38bdf8', icon }: StatCardProps) {
  return (
    <div className="card metric-card" style={{ minHeight: 150 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <span style={{ color: '#94a3b8', fontSize: 13 }}>{title}</span>
        <div style={{ width: 36, height: 36, borderRadius: 12, display: 'flex', alignItems: 'center', justifyContent: 'center', background: accent, color: '#020817' }}>
          {icon}
        </div>
      </div>
      <div style={{ fontSize: 32, fontWeight: 700, marginBottom: 8 }}>{value}</div>
      <div style={{ color: '#94a3b8', fontSize: 13 }}>{subtitle}</div>
      {trend ? <div style={{ color: '#22c55e', fontSize: 12, marginTop: 8 }}>{trend}</div> : null}
    </div>
  );
}
