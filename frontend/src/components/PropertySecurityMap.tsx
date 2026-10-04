import { Clock3, Cpu, MapPin, TriangleAlert } from 'lucide-react';
import { useMemo, useState } from 'react';
import type { DeviceSummary, SecurityEvent } from '../types';

function getPosition(location: string, index: number) {
  const name = location.toLowerCase();
  if (/main door|main entrance|front|entry|gate/.test(name)) return { left: 80, top: 76 };
  if (/garage/.test(name)) return { left: 18, top: 76 };
  if (/bedroom|sleep/.test(name)) return { left: 78, top: 24 };
  if (/living|lounge/.test(name)) return { left: 49, top: 52 };
  if (/kitchen/.test(name)) return { left: 78, top: 52 };
  if (/back|rear|side/.test(name)) return { left: 18, top: 24 };
  return { left: 32 + (index % 3) * 20, top: 28 + (Math.floor(index / 3) % 2) * 37 };
}

export default function PropertySecurityMap({ propertyId, devices, events }: { propertyId: string; devices: DeviceSummary[]; events: SecurityEvent[] }) {
  const [selectedDeviceId, setSelectedDeviceId] = useState('');
  const propertyEvents = events.filter(event => event.propertyId === propertyId).sort((left, right) => new Date(left.timestamp).getTime() - new Date(right.timestamp).getTime());
  const propertyDevices = devices.filter(device => device.propertyId === propertyId);
  const sensorMarkers = propertyDevices.map((device, index) => {
    const deviceEvents = propertyEvents.filter(event => event.deviceId === device.deviceId).sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime());
    const latest = deviceEvents[0];
    return { device, latest, position: getPosition(latest?.location || device.sensorType, index), stale: Date.now() - new Date(device.lastSeen).getTime() > 35 * 60 * 1000 };
  });
  const selectedDevice = sensorMarkers.find(marker => marker.device.deviceId === selectedDeviceId);
  const sequences = useMemo(() => {
    const groups: SecurityEvent[][] = [];
    for (const event of propertyEvents) {
      const lastGroup = groups[groups.length - 1];
      const prior = lastGroup?.[lastGroup.length - 1];
      const gap = prior ? new Date(event.timestamp).getTime() - new Date(prior.timestamp).getTime() : Infinity;
      if (lastGroup && prior?.propertyId === event.propertyId && gap >= 0 && gap <= 5 * 60 * 1000) lastGroup.push(event);
      else groups.push([event]);
    }
    return groups.filter(group => group.length > 1).sort((left, right) => new Date(right[right.length - 1].timestamp).getTime() - new Date(left[left.length - 1].timestamp).getTime()).slice(0, 4);
  }, [propertyEvents]);

  if (!propertyId) return null;
  return <div className="map-sequence-grid">
    <section className="intel-panel property-map-panel">
      <div className="intel-section-heading"><div><p className="eyebrow">PROPERTY SECURITY MAP</p><h3>Sensor locations</h3></div><MapPin size={16} /></div>
      <p className="map-disclosure">Schematic placement is based on each sensor’s recorded location. Device state is inferred from event recency, not a live heartbeat.</p>
      <div className="property-floorplan" aria-label="Schematic property floor plan">
        <div className="floor-room floor-bedroom"><span>BEDROOM</span></div><div className="floor-room floor-living"><span>LIVING ROOM</span></div><div className="floor-room floor-kitchen"><span>KITCHEN</span></div><div className="floor-room floor-garage"><span>GARAGE</span></div><div className="floor-room floor-entry"><span>MAIN ENTRY</span></div>
        {sensorMarkers.map(marker => <button key={marker.device.deviceId} className={`floor-sensor ${marker.stale ? 'sensor-quiet' : marker.latest?.priority === 'High' || marker.latest?.priority === 'Critical' ? 'sensor-priority' : 'sensor-recent'} ${selectedDeviceId === marker.device.deviceId ? 'selected' : ''}`} style={{ left: `${marker.position.left}%`, top: `${marker.position.top}%` }} title={`${marker.latest?.location || marker.device.sensorType}: ${marker.device.deviceId}`} onClick={() => setSelectedDeviceId(marker.device.deviceId)} aria-label={`Show ${marker.device.deviceId} sensor details`}><Cpu size={13} /></button>)}
      </div>
      {selectedDevice && <div className="sensor-detail-card"><div><strong>{selectedDevice.latest?.location || selectedDevice.device.sensorType}</strong><span>{selectedDevice.device.deviceId} · {selectedDevice.device.sensorType}</span></div><b className={selectedDevice.stale ? 'sensor-label-quiet' : 'sensor-label-recent'}>{selectedDevice.stale ? 'No recent event' : 'Recent event'}</b><p>{selectedDevice.latest ? `${selectedDevice.latest.eventType.replace(/([A-Z])/g, ' $1').trim()} · ${new Date(selectedDevice.latest.timestamp).toLocaleString()}` : 'No recorded event for this sensor yet.'}</p>{selectedDevice.latest?.description && <small>{selectedDevice.latest.description}</small>}</div>}
      {!propertyDevices.length && <div className="empty-state">Sensors appear here when events have been recorded.</div>}
    </section>
    <section className="intel-panel event-sequences-panel">
      <div className="intel-section-heading"><div><p className="eyebrow">EVENT CORRELATION</p><h3>Related activity sequences</h3></div><TriangleAlert size={16} /></div>
      <p className="map-disclosure">Events from the same property, within five minutes of each other, are grouped into a review sequence.</p>
      {!sequences.length && <div className="empty-state">Related events will appear here when they occur close together.</div>}
      {sequences.map((sequence, groupIndex) => <article className="event-sequence" key={`${sequence[0].id}-${groupIndex}`}><strong>{sequence.length} related events · {sequence[0].location || sequence[0].sensorType}</strong><div>{sequence.map((event, index) => <div className="sequence-event" key={event.id}><span className={`priority-dot priority-${event.priority.toLowerCase()}`} /><time>{new Date(event.timestamp).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', second: '2-digit' })}</time><span>{event.eventType.replace(/([A-Z])/g, ' $1').trim()} · {event.location || event.sensorType}</span>{index < sequence.length - 1 && <i />}</div>)}</div><small><Clock3 size={12} />{new Date(sequence[0].timestamp).toLocaleDateString()}</small></article>)}
    </section>
  </div>;
}
