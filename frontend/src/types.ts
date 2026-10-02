export type PropertyStatus = 'Home' | 'Away' | 'Vacation' | 'Maintenance';

export type AlertPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type AlertStatus = 'Unread' | 'Read' | 'Acknowledged' | 'Resolved';

export interface Property {
  id: string;
  name: string;
  address: string;
  owner: string;
  ownerUserId?: number | null;
  photoUrl?: string | null;
  status: PropertyStatus;
  createdAt: string;
  updatedAt: string;
}

export interface PropertyCreateRequest {
  name: string;
  address: string;
  owner: string;
  ownerUserId?: number | null;
  status: PropertyStatus;
}

export interface SecurityEvent {
  id: string;
  deviceId: string;
  propertyId: string;
  sensorType: string;
  eventType: string;
  location: string;
  description: string;
  priority: AlertPriority;
  status: string;
  timestamp: string;
  createdAt: string;
}

export interface DeviceSummary {
  deviceId: string;
  propertyId: string;
  sensorType: string;
  lastSeen: string;
  lastEventType: string;
  status: string;
}

export interface AlertItem {
  id: string;
  securityEventId: string;
  propertyId: string;
  priority: AlertPriority;
  message: string;
  status: AlertStatus;
  createdAt: string;
  readAt?: string | null;
}

export interface DashboardOverview {
  totalProperties: number;
  totalEvents: number;
  openAlerts: number;
  totalDevices: number;
  riskScore: number;
  criticalAlertCount: number;
  recentEvents: SecurityEvent[];
  recentAlerts: AlertItem[];
}

export interface AuthUser {
  id: number;
  fullName: string;
  email: string;
  profilePhotoUrl?: string | null;
  role: string;
  isBlocked: boolean;
  lastLoginAt?: string | null;
}

export interface UserReport {
  id: number;
  recipientUserId: number;
  senderUserId: number;
  title: string;
  body: string;
  createdAt: string;
  isRead: boolean;
  readAt?: string | null;
}

export interface AdminUserDetails {
  user: AuthUser & { createdAt: string; lastLoginIp?: string | null };
  properties: Property[];
  events: SecurityEvent[];
  alerts: AlertItem[];
  reports: UserReport[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  user: AuthUser;
}

export interface RegisterResponse {
  message: string;
  user?: AuthUser;
}

export interface UpdateProfileRequest {
  fullName: string;
  email: string;
  currentPassword: string;
  newPassword: string;
}

export interface SystemSetting {
  name: string;
  value: string;
  updatedAt: string;
}

export interface ToggleUserBlockRequest {
  isBlocked: boolean;
}
