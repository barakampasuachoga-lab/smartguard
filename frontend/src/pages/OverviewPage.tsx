import { Activity, ArrowDownRight, ArrowUpRight, Bell, Building2, Clock3, Cpu, Plus, Save, ShieldCheck, Trash2, TriangleAlert } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import PropertyAccessPanel from '../components/PropertyAccessPanel';
import PropertySecurityMap from '../components/PropertySecurityMap';
import { getAlerts, getDevices, getEvents, getOverview, getProperties, getSecuritySchedule, updateSecuritySchedule } from '../api';
import type { AlertItem, DashboardOverview, DeviceSummary, Property, SecurityEvent, SecuritySchedule } from '../types';

const hours = 24 * 60 * 60 * 1000;
const isOpen = (alert: AlertItem) => alert.status === 'Unread' || alert.status === 'Read';
const isAnomaly = (event: SecurityEvent) => event.eventType === 'PotentiallyUnusualActivity' || event.eventType === 'DeviceOffline';
const defaultSchedule: SecuritySchedule = { periods: [{ name: 'Morning', start: '06:00', end: '09:00' }, { name: 'Day', start: '09:00', end: '17:00' }, { name: 'Evening', start: '17:00', end: '22:00' }, { name: 'Night', start: '22:00', end: '06:00' }], routines: [] };

export default function OverviewPage() {
  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [events, setEvents] = useState<SecurityEvent[]>([]);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [devices, setDevices] = useState<DeviceSummary[]>([]);
  const [properties, setProperties] = useState<Property[]>([]);
  const [selectedPropertyId, setSelectedPropertyId] = useState('');
  const [schedule, setSchedule] = useState<SecuritySchedule>(defaultSchedule);
  const [scheduleError, setScheduleError] = useState('');
  const [scheduleSaved, setScheduleSaved] = useState(false);
  const [savingSchedule, setSavingSchedule] = useState(false);
  const [error, setError] = useState('');

  const loadIntelligence = useCallback(() => {
    void Promise.all([getOverview(), getEvents(), getAlerts(), getDevices(), getProperties()])
      .then(([summary, securityEvents, securityAlerts, deviceSummaries, propertyList]) => {
        setOverview(summary);
        setEvents(securityEvents);
        setAlerts(securityAlerts);
        setDevices(deviceSummaries);
        setProperties(propertyList);
        setSelectedPropertyId(current => current && propertyList.some(property => property.id === current) ? current : propertyList[0]?.id ?? '');
      })
      .catch(() => setError('Unable to load your security intelligence. Please try again.'));
  }, []);

  useEffect(() => {
    loadIntelligence();
    const refreshTimer = window.setInterval(loadIntelligence, 30_000);
    return () => window.clearInterval(refreshTimer);
  }, [loadIntelligence]);

  useEffect(() => {
    if (!selectedPropertyId) return;
    setScheduleError('');
    void getSecuritySchedule(selectedPropertyId).then(setSchedule).catch(() => setScheduleError('Unable to load this property’s schedule.'));
  }, [selectedPropertyId]);

  const saveSchedule = async () => {
    if (!selectedPropertyId) return;
    try {
      setSavingSchedule(true);
      setScheduleError('');
      await updateSecuritySchedule(selectedPropertyId, schedule);
      setScheduleSaved(true);
      window.setTimeout(() => setScheduleSaved(false), 2500);
    } catch {
      setScheduleError('Unable to save the security schedule. Check that all times are valid.');
    } finally { setSavingSchedule(false); }
  };

  const now = Date.now();
  const openAlerts = alerts.filter(isOpen);
  const recentEvents = events.filter(event => now - new Date(event.timestamp).getTime() <= hours);
  const anomalyEvents = events.filter(isAnomaly);
  const staleDevices = devices.filter(device => now - new Date(device.lastSeen).getTime() > 35 * 60 * 1000);
  const propertyEvents = events.filter(event => event.propertyId === selectedPropertyId);
  const propertyAnomalies = propertyEvents.filter(isAnomaly);
  const propertyRecentEvents = recentEvents.filter(event => event.propertyId === selectedPropertyId);
  const propertyAlerts = alerts.filter(alert => alert.propertyId === selectedPropertyId);
  const propertyOpenAlerts = propertyAlerts.filter(isOpen);
  const propertyDevices = devices.filter(device => device.propertyId === selectedPropertyId);
  const propertyQuietDevices = propertyDevices.filter(device => now - new Date(device.lastSeen).getTime() > 35 * 60 * 1000);
  const propertyAcknowledgedAlerts = propertyAlerts.filter(alert => alert.acknowledgedAt);
  const avgResponseMs = propertyAcknowledgedAlerts.length
    ? propertyAcknowledgedAlerts.reduce((total, alert) => total + (new Date(alert.acknowledgedAt!).getTime() - new Date(alert.createdAt).getTime()), 0) / propertyAcknowledgedAlerts.length
    : null;
  const scoreFactors = useMemo(() => {
    const factors: { label: string; detail: string; points: number; tone: string }[] = [];
    if (propertyOpenAlerts.length) factors.push({ label: 'Unresolved alerts', detail: `${propertyOpenAlerts.length} alert${propertyOpenAlerts.length === 1 ? '' : 's'} still need review for this property`, points: Math.min(36, propertyOpenAlerts.reduce((sum, alert) => sum + (alert.priority === 'Critical' ? 18 : alert.priority === 'High' ? 12 : alert.priority === 'Medium' ? 7 : 3), 0)), tone: 'warning' });
    const recentHighPriority = propertyRecentEvents.filter(event => event.priority === 'Critical' || event.priority === 'High');
    if (recentHighPriority.length) factors.push({ label: 'High priority activity', detail: `${recentHighPriority.length} high or critical event${recentHighPriority.length === 1 ? '' : 's'} in the last 24 hours`, points: Math.min(25, recentHighPriority.reduce((sum, event) => sum + (event.priority === 'Critical' ? 12 : 6), 0)), tone: 'warning' });
    if (propertyQuietDevices.length) factors.push({ label: 'Quiet device signals', detail: `${propertyQuietDevices.length} device${propertyQuietDevices.length === 1 ? '' : 's'} have not logged an event in over 35 minutes`, points: Math.min(20, propertyQuietDevices.length * 8), tone: 'muted' });
    if (propertyRecentEvents.length > 10) factors.push({ label: 'Event frequency', detail: `${propertyRecentEvents.length} events recorded in the last 24 hours`, points: Math.min(15, propertyRecentEvents.length - 10), tone: 'muted' });
    if (avgResponseMs !== null && avgResponseMs > 5 * 60 * 1000) factors.push({ label: 'Alert response time', detail: `Average acknowledgement took ${Math.round(avgResponseMs / 60000)} minutes`, points: Math.min(15, Math.floor(avgResponseMs / 300000) * 3), tone: 'warning' });
    if (!factors.length) factors.push({ label: 'No current concerns', detail: 'No open alerts, recent high priority events, or quiet device signals.', points: 0, tone: 'good' });
    return factors;
  }, [propertyOpenAlerts, propertyRecentEvents, propertyQuietDevices, avgResponseMs]);
  const score = Math.max(0, 100 - scoreFactors.reduce((sum, factor) => sum + factor.points, 0));
  const scoreStatus = score >= 80 ? 'LOOKING GOOD' : score >= 55 ? 'NEEDS ATTENTION' : 'ACTION RECOMMENDED';
  const scoreTone = score >= 80 ? 'good' : score >= 55 ? 'warning' : 'critical';
  const propertyById = new Map(properties.map(property => [property.id, property]));
  const eventById = new Map(events.map(event => [event.id, event]));
  const hotspots = Object.entries(propertyEvents.reduce<Record<string, number>>((counts, event) => {
    const location = event.location?.trim() || 'Unspecified area';
    counts[location] = (counts[location] ?? 0) + 1;
    return counts;
  }, {})).sort((a, b) => b[1] - a[1]).slice(0, 5);
  const maxHotspot = hotspots[0]?.[1] ?? 1;
  const timeline = [...events].sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()).slice(0, 8);
  const chartData = Array.from({ length: 7 }, (_, index) => {
    const day = new Date();
    day.setHours(0, 0, 0, 0);
    day.setDate(day.getDate() - (6 - index));
    const nextDay = new Date(day);
    nextDay.setDate(day.getDate() + 1);
    return {
      name: day.toLocaleDateString(undefined, { weekday: 'short' }),
      events: propertyEvents.filter(event => {
        const timestamp = new Date(event.timestamp).getTime();
        return timestamp >= day.getTime() && timestamp < nextDay.getTime();
      }).length,
    };
  });
  const eventTypeCounts = Object.entries(propertyEvents.reduce<Record<string, number>>((counts, event) => {
    counts[event.eventType] = (counts[event.eventType] ?? 0) + 1;
    return counts;
  }, {})).sort((left, right) => right[1] - left[1]);
  const responseLabel = avgResponseMs === null ? 'No response data' : avgResponseMs < 60000
    ? `${Math.round(avgResponseMs)} sec`
    : `${Math.floor(avgResponseMs / 60000)} min ${Math.round((avgResponseMs % 60000) / 1000)} sec`;
  const thisWeekStart = new Date();
  thisWeekStart.setHours(0, 0, 0, 0);
  thisWeekStart.setDate(thisWeekStart.getDate() - 6);
  const previousWeekStart = new Date(thisWeekStart);
  previousWeekStart.setDate(previousWeekStart.getDate() - 7);
  const thisWeekEvents = propertyEvents.filter(event => new Date(event.timestamp).getTime() >= thisWeekStart.getTime());
  const previousWeekEvents = propertyEvents.filter(event => {
    const timestamp = new Date(event.timestamp).getTime();
    return timestamp >= previousWeekStart.getTime() && timestamp < thisWeekStart.getTime();
  });
  const eventChange = thisWeekEvents.length - previousWeekEvents.length;
  const busiestLocation = hotspots[0];
  const recommendations: string[] = [];
  if (eventChange > 0 && previousWeekEvents.length > 0) recommendations.push(`Recorded events increased by ${eventChange} compared with the previous seven-day period. Review the recent timeline for repeated activity.`);
  if (busiestLocation && busiestLocation[1] >= 3) recommendations.push(`${busiestLocation[0]} is the most active area with ${busiestLocation[1]} recorded events. Review sensor placement and the configured access schedule.`);
  if (propertyQuietDevices.length) recommendations.push(`${propertyQuietDevices.length} device${propertyQuietDevices.length === 1 ? ' has' : 's have'} not logged an event for over 35 minutes. Check power and network connectivity.`);
  if (propertyOpenAlerts.length) recommendations.push(`${propertyOpenAlerts.length} alert${propertyOpenAlerts.length === 1 ? ' remains' : 's remain'} unresolved. Review and update their incident status.`);
  if (!recommendations.length) recommendations.push('No elevated patterns stand out in the available records. Continue monitoring and keep device schedules current.');
  const eventTypesForProperty = Object.entries(propertyEvents.reduce<Record<string, number>>((counts, event) => {
    counts[event.eventType] = (counts[event.eventType] ?? 0) + 1;
    return counts;
  }, {})).sort((a, b) => b[1] - a[1]);
  const busiestHour = (() => {
    const counts = propertyEvents.reduce<Record<number, number>>((result, event) => {
      const hour = new Date(event.timestamp).getHours();
      result[hour] = (result[hour] ?? 0) + 1;
      return result;
    }, {});
    const peak = Object.entries(counts).sort((a, b) => b[1] - a[1])[0];
    return peak ? `${new Date(2000, 0, 1, Number(peak[0])).toLocaleTimeString([], { hour: 'numeric' })}–${new Date(2000, 0, 1, (Number(peak[0]) + 2) % 24).toLocaleTimeString([], { hour: 'numeric' })}` : 'No events';
  })();

  return (
    <div className="intelligence-page">
      {error && <p className="form-error" role="alert">{error}</p>}
      <section className="intelligence-intro">
        <div><p className="eyebrow">SMARTGUARD / SECURITY INTELLIGENCE</p><h2>A clearer picture of your properties.</h2><p>Recent activity, alert context, and device reporting in one place.</p></div>
        <Link className="primary-button" to="/alerts"><Bell size={15} /> Review alerts{openAlerts.length > 0 && <span className="intel-button-count">{openAlerts.length}</span>}</Link>
      </section>

      <section className="intel-panel intel-schedule-panel">
        <div className="intel-section-heading"><div><p className="eyebrow">SCHEDULE & BEHAVIOR LEARNING</p><h3>Set expected activity</h3></div><div className="schedule-save-state">{scheduleSaved && <span>Schedule saved</span>}<button className="subtle-button" type="button" onClick={() => void saveSchedule()} disabled={savingSchedule || !selectedPropertyId}><Save size={13} />{savingSchedule ? 'Saving…' : 'Save schedule'}</button></div></div>
        <div className="schedule-property-select"><label htmlFor="schedule-property">Property</label><select id="schedule-property" value={selectedPropertyId} onChange={event => setSelectedPropertyId(event.target.value)}>{properties.map(property => <option value={property.id} key={property.id}>{property.name}</option>)}</select><span>Times use this server’s local timezone.</span></div>
        {scheduleError && <p className="form-error" role="alert">{scheduleError}</p>}
        <div className="schedule-period-grid">{schedule.periods.map((period, index) => <div className="schedule-period" key={period.name}><strong>{period.name}</strong><label>From<input type="time" value={period.start} onChange={event => setSchedule(current => ({ ...current, periods: current.periods.map((item, i) => i === index ? { ...item, start: event.target.value } : item) }))} /></label><label>Until<input type="time" value={period.end} onChange={event => setSchedule(current => ({ ...current, periods: current.periods.map((item, i) => i === index ? { ...item, end: event.target.value } : item) }))} /></label></div>)}</div>
        <div className="schedule-routines-heading"><div><strong>Usual routines</strong><span>Events outside a 45-minute routine window can be flagged.</span></div><button className="subtle-button" type="button" onClick={() => setSchedule(current => ({ ...current, routines: [...current.routines, { days: 'Monday-Friday', time: '08:00', eventType: 'DoorOpened', label: 'Morning entry' }] }))}><Plus size={13} /> Add routine</button></div>
        {!schedule.routines.length && <p className="schedule-empty">Add an expected door, window, or motion event to start comparing activity with your normal routine.</p>}
        {schedule.routines.map((routine, index) => <div className="schedule-routine-row" key={`${index}-${routine.eventType}`}><select aria-label="Routine days" value={routine.days} onChange={event => setSchedule(current => ({ ...current, routines: current.routines.map((item, i) => i === index ? { ...item, days: event.target.value } : item) }))}><option>Monday-Friday</option><option>Weekends</option><option>Everyday</option><option>Monday</option><option>Tuesday</option><option>Wednesday</option><option>Thursday</option><option>Friday</option><option>Saturday</option><option>Sunday</option></select><input aria-label="Routine time" type="time" value={routine.time} onChange={event => setSchedule(current => ({ ...current, routines: current.routines.map((item, i) => i === index ? { ...item, time: event.target.value } : item) }))} /><select aria-label="Expected activity" value={routine.eventType} onChange={event => setSchedule(current => ({ ...current, routines: current.routines.map((item, i) => i === index ? { ...item, eventType: event.target.value } : item) }))}><option value="DoorOpened">Door opened</option><option value="WindowOpened">Window opened</option><option value="MotionDetected">Motion detected</option></select><input aria-label="Routine label" value={routine.label} placeholder="Routine name" onChange={event => setSchedule(current => ({ ...current, routines: current.routines.map((item, i) => i === index ? { ...item, label: event.target.value } : item) }))} /><button className="icon-button" type="button" aria-label="Remove routine" onClick={() => setSchedule(current => ({ ...current, routines: current.routines.filter((_, i) => i !== index) }))}><Trash2 size={15} /></button></div>)}
      </section>

      <section className="intel-overview-grid">
        <article className={`intel-score-card score-${scoreTone}`}>
          <div className="intel-score-top"><span><ShieldCheck size={16} /> PROPERTY SECURITY SCORE</span><span className={`intel-status status-${scoreTone}`}>{scoreStatus}</span></div>
          <div className="intel-score-main"><strong>{score}</strong><span>/ 100</span><small>{propertyById.get(selectedPropertyId)?.name ?? 'Property'} posture</small></div>
          <div className="intel-score-meter"><span style={{ width: `${score}%` }} /></div>
          <p>This score starts at 100 and deducts points for unresolved alerts, recent high priority events, and devices with quiet event histories.</p>
        </article>
        <div className="intel-stat-grid">
          <article className="intel-stat"><span>Properties</span><strong><Building2 size={17} />{overview?.totalProperties ?? '—'}</strong><small>In your workspace</small></article>
          <article className="intel-stat"><span>Potential anomalies</span><strong><TriangleAlert size={17} />{anomalyEvents.length}</strong><small>Flagged event records</small></article>
          <article className="intel-stat"><span>Open alerts</span><strong><Bell size={17} />{openAlerts.length}</strong><small>Awaiting review</small></article>
          <article className="intel-stat"><span>Recent device signals</span><strong><Cpu size={17} />{devices.length - staleDevices.length}<small> / {devices.length}</small></strong><small>Events within 35 minutes</small></article>
        </div>
      </section>

      <section className="intel-panel intel-score-factors">
        <div className="intel-section-heading"><div><p className="eyebrow">SCORE BREAKDOWN</p><h3>What shaped the score</h3></div><span>Refreshed {new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</span></div>
        <div className="intel-factor-list">{scoreFactors.map(factor => <article className={`intel-factor factor-${factor.tone}`} key={factor.label}><span className="intel-factor-icon">{factor.points ? <ArrowDownRight size={16} /> : <ArrowUpRight size={16} />}</span><div><strong>{factor.label}</strong><p>{factor.detail}</p></div><b>{factor.points ? `−${factor.points}` : 'Clear'}</b></article>)}</div>
      </section>

      <section className="intel-panel intel-chart-panel">
        <div className="intel-section-heading"><div><p className="eyebrow">ACTIVITY ANALYSIS</p><h3>Security events · last 7 days</h3></div><span>{propertyEvents.length} property records</span></div>
        <div className="intel-chart-wrap">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={chartData} margin={{ top: 8, right: 10, left: -18, bottom: 0 }}>
              <defs><linearGradient id="intelEventFill" x1="0" x2="0" y1="0" y2="1"><stop offset="5%" stopColor="#d5f477" stopOpacity={0.35} /><stop offset="95%" stopColor="#d5f477" stopOpacity={0.02} /></linearGradient></defs>
              <CartesianGrid stroke="rgba(195,214,196,.12)" vertical={false} />
              <XAxis dataKey="name" stroke="#91a095" tickLine={false} axisLine={false} tick={{ fontSize: 9 }} />
              <YAxis allowDecimals={false} stroke="#91a095" tickLine={false} axisLine={false} tick={{ fontSize: 9 }} />
              <Tooltip contentStyle={{ background: '#101916', border: '1px solid rgba(195,214,196,.2)', borderRadius: 5, color: '#e7eee7', fontSize: 10 }} />
              <Area type="monotone" dataKey="events" name="Events" stroke="#d5f477" fill="url(#intelEventFill)" strokeWidth={2} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </section>

      <section className="intel-insight-strip">
        <article><span>Most common event</span><strong>{eventTypeCounts[0]?.[0].replace(/([A-Z])/g, ' $1').trim() ?? 'No events'}</strong><small>{eventTypeCounts[0] ? `${eventTypeCounts[0][1]} recorded` : 'Awaiting activity'}</small></article>
        <article><span>Average acknowledgement</span><strong>{responseLabel}</strong><small>From recorded alert acknowledgements</small></article>
        <article><span>Most active period</span><strong>{(() => { const hoursCount = propertyEvents.reduce<Record<number, number>>((counts, event) => { const hour = new Date(event.timestamp).getHours(); counts[hour] = (counts[hour] ?? 0) + 1; return counts; }, {}); const peak = Object.entries(hoursCount).sort((left, right) => Number(right[1]) - Number(left[1]))[0]; return peak ? `${String(Number(peak[0])).padStart(2, '0')}:00–${String((Number(peak[0]) + 1) % 24).padStart(2, '0')}:00` : 'No events'; })()}</strong><small>Based on recorded event times</small></article>
      </section>

      <section className="intel-risk-grid">
        <article className="intel-panel intel-risk-panel">
          <div className="intel-section-heading"><div><p className="eyebrow">HISTORICAL PATTERN REVIEW</p><h3>Security risk analysis</h3></div><span className={`intel-status ${propertyOpenAlerts.length || propertyQuietDevices.length ? 'status-warning' : 'status-good'}`}>{propertyOpenAlerts.length || propertyQuietDevices.length ? 'REVIEW' : 'STEADY'}</span></div>
          <p className="intel-risk-summary">{eventChange > 0 ? `Recorded activity is up ${eventChange} event${eventChange === 1 ? '' : 's'} versus the previous seven days.` : eventChange < 0 ? `Recorded activity is down ${Math.abs(eventChange)} event${Math.abs(eventChange) === 1 ? '' : 's'} versus the previous seven days.` : 'Recorded activity is level with the previous seven days.'}</p>
          <div className="intel-risk-comparison"><div><span>Last 7 days</span><strong>{thisWeekEvents.length}</strong><small>security events</small></div><div><span>Previous 7 days</span><strong>{previousWeekEvents.length}</strong><small>security events</small></div><div><span>Anomalies recorded</span><strong>{propertyAnomalies.length}</strong><small>all available history</small></div></div>
          <div className="intel-recommendations"><strong>Recommended checks</strong>{recommendations.map((recommendation, index) => <p key={index}><span>{index + 1}</span>{recommendation}</p>)}</div>
          <small className="intel-data-note">This is a historical activity assessment, not a prediction of crime. Recommendations use recorded event and alert data.</small>
        </article>
        <article className="intel-panel intel-device-health">
          <div className="intel-section-heading"><div><p className="eyebrow">IOT DEVICE HEALTH CENTER</p><h3>Reporting devices</h3></div><span>{propertyDevices.length} known</span></div>
          {!propertyDevices.length && <div className="empty-state">Device records appear after sensors report security events.</div>}
          {propertyDevices.map(device => {
            const ageMinutes = Math.max(0, Math.floor((now - new Date(device.lastSeen).getTime()) / 60000));
            const isQuiet = ageMinutes > 35;
            const statusLabel = isQuiet ? 'No recent event' : 'Recently active';
            return <div className="device-health-row" key={device.deviceId}><span className={`device-health-dot ${isQuiet ? 'quiet' : 'active'}`} /><div><strong>{device.deviceId}</strong><small>{device.sensorType} · {device.lastEventType.replace(/([A-Z])/g, ' $1').trim()}</small></div><span className={isQuiet ? 'device-health-quiet' : 'device-health-active'}>{statusLabel}</span><time>{isQuiet ? `${ageMinutes}m ago` : `${ageMinutes}m ago`}</time></div>;
          })}
          <small className="intel-data-note">Reporting state is estimated from the last event. Live heartbeat, battery, and signal-strength telemetry are not configured.</small>
        </article>
      </section>

      <PropertySecurityMap propertyId={selectedPropertyId} devices={devices} events={events} />
      {selectedPropertyId && <PropertyAccessPanel propertyId={selectedPropertyId} />}

      <div className="intel-detail-grid">
        <section className="intel-panel intel-timeline">
          <div className="intel-section-heading"><div><p className="eyebrow">PROPERTY ACTIVITY</p><h3>Security timeline</h3></div><Link to="/activities">All events <ArrowUpRight size={14} /></Link></div>
          {!timeline.length && <div className="empty-state">No security events have been recorded yet.</div>}
          <div className="intel-timeline-list">{timeline.map(event => <article className="intel-timeline-item" key={event.id}><span className={`priority-dot priority-${event.priority.toLowerCase()}`} /><time>{new Date(event.timestamp).toLocaleString([], { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}</time><div><strong>{event.eventType.replace(/([A-Z])/g, ' $1').trim()}</strong><span>{event.location || event.sensorType} · {propertyById.get(event.propertyId)?.name ?? 'Property'}</span>{isAnomaly(event) && <p className="intel-explain"><TriangleAlert size={13} />{event.description || 'This event was flagged as unusual and needs review.'}</p>}</div></article>)}</div>
        </section>
        <div className="intel-side-stack">
          <section className="intel-panel intel-anomalies"><div className="intel-section-heading"><div><p className="eyebrow">EXPLAINABLE DETECTION</p><h3>Why it was flagged</h3></div></div>
            {!openAlerts.length && <div className="intel-clear-state"><ShieldCheck size={17} /><span>No unresolved anomaly alerts.</span></div>}
            {openAlerts.slice(0, 4).map(alert => { const event = eventById.get(alert.securityEventId); const property = propertyById.get(alert.propertyId); return <article className="intel-alert-reason" key={alert.id}><div><span className={`intel-priority priority-${alert.priority.toLowerCase()}`}>{alert.priority}</span><time>{new Date(alert.createdAt).toLocaleString([], { hour: 'numeric', minute: '2-digit' })}</time></div><strong>{property?.name ?? 'Property'}{event?.location ? ` · ${event.location}` : ''}</strong><p>{alert.message}</p>{event && <small>Signal: {event.eventType.replace(/([A-Z])/g, ' $1').trim()} · {event.status}</small>}</article>; })}
          </section>
          <section className="intel-panel intel-heatmap"><div className="intel-section-heading"><div><p className="eyebrow">EVENT DISTRIBUTION</p><h3>Most active areas</h3></div><Activity size={16} /></div>
            {!hotspots.length && <div className="empty-state">Location activity will appear here.</div>}
            {hotspots.map(([location, count]) => <div className="intel-heat-row" key={location}><span>{location}</span><div><i style={{ width: `${Math.max(8, (count / maxHotspot) * 100)}%` }} /></div><b>{count}</b></div>)}
            <p className="intel-data-note"><Clock3 size={12} /> Device recency is based on the latest recorded event. Hardware heartbeat and battery telemetry are not currently available.</p>
          </section>
        </div>
      </div>
    </div>
  );
}
