import { useEffect, useState } from 'react';
import { Activity, Bell, Gauge, Wrench } from 'lucide-react';
import { getSettings, updateSetting } from '../api';
import type { SystemSetting } from '../types';

const settingDetails = [
  { name: 'anomalyDetectionEnabled', label: 'Anomaly detection', description: 'Evaluate incoming activity against the configured detection rules.', icon: Activity },
  { name: 'alertNotificationsEnabled', label: 'Alert notifications', description: 'Allow the platform to surface priority alert notifications.', icon: Bell },
  { name: 'maintenanceMode', label: 'Maintenance mode', description: 'Mark the system as undergoing scheduled maintenance.', icon: Wrench },
  { name: 'riskThreshold', label: 'Risk score threshold', description: 'Risk scores at or above this value are treated as high priority.', icon: Gauge },
];

export default function SettingsPage() {
  const [settings, setSettings] = useState<SystemSetting[]>([]);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState<string | null>(null);

  useEffect(() => {
    void getSettings().then(setSettings).catch(() => setError('Unable to load system settings.'));
  }, []);

  const changeSetting = async (name: string, value: string) => {
    setSaving(name);
    setError('');
    try {
      const updated = await updateSetting(name, value);
      setSettings(current => current.map(setting => setting.name === name ? updated : setting));
    } catch {
      setError('Unable to save this system setting.');
    } finally {
      setSaving(null);
    }
  };

  return (
    <section className="workspace-section settings-page">
      <div className="workspace-section-heading"><div><p className="eyebrow">Administrator controls</p><h2>System settings</h2></div><span className="role-tag role-admin">Administrator</span></div>
      <div className="settings-list">
        {settingDetails.map(({ name, label, description, icon: Icon }) => {
          const value = settings.find(setting => setting.name === name)?.value;
          const isBoolean = name !== 'riskThreshold';
          return <article className="setting-row" key={name}>
            <div className="setting-icon"><Icon size={18} /></div>
            <div className="setting-copy"><strong>{label}</strong><span>{description}</span></div>
            {isBoolean ? <label className="setting-toggle" aria-label={label}>
              <input type="checkbox" checked={value === 'true'} disabled={saving === name || value === undefined} onChange={event => void changeSetting(name, String(event.target.checked))} />
              <span />
            </label> : <label className="threshold-input"><input type="number" min="0" max="100" value={value ?? ''} disabled={saving === name} onChange={event => setSettings(current => current.map(setting => setting.name === name ? { ...setting, value: event.target.value } : setting))} onBlur={event => { if (event.target.value) void changeSetting(name, event.target.value); }} /><span>/ 100</span></label>}
          </article>;
        })}
      </div>
      {error && <p className="form-error" role="alert">{error}</p>}
    </section>
  );
}