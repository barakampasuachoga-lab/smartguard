export type PropertyStatus = 'Home' | 'Away' | 'Sleep' | 'Night' | 'Vacation' | 'Maintenance';

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

export interface SecuritySchedulePeriod { name: string; start: string; end: string }
export interface SecurityRoutine { days: string; time: string; eventType: string; label: string }
export interface SecuritySchedule { periods: SecuritySchedulePeriod[]; routines: SecurityRoutine[] }
export interface TrustedContact { id: number; propertyId: string; name: string; relationship: string; email?: string | null; days: string; startTime: string; endTime: string; isActive: boolean }
export interface SecurityCheckIn { id: string; propertyId: string; userId: number; userName: string; note?: string | null; createdAt: string }

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
  alertType?: string;
  message: string;
  propertyName?: string;
  status: AlertStatus;
  createdAt: string;
  readAt?: string | null;
  acknowledgedByUserId?: number | null;
  acknowledgedBy?: string | null;
  acknowledgedAt?: string | null;
  acknowledgementComment?: string | null;
  resolvedByUserId?: number | null;
  resolvedBy?: string | null;
  resolvedAt?: string | null;
  resolutionNote?: string | null;
  escalatedAt?: string | null;
  escalationDetails?: string | null;
  securityEvent?: SecurityEvent | null;
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
  subscription: UserSubscription | null;
  subscriptionPlan: SubscriptionPlan | null;
  paymentTransactions: PaymentTransaction[];
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

export interface SubscriptionPlan {
  code: string;
  name: string;
  priceKes: number;
  maxProperties: number;
  maxDevices: number;
  anomalyDetection: boolean;
  analytics: boolean;
  incidentManagement: boolean;
  securityIntelligence: boolean;
  multipleStaffAccounts: boolean;
  advancedReports: boolean;
  prioritySupport: boolean;
}

export interface UserSubscription {
  id: number;
  userId: number;
  planCode: string;
  status: 'ACTIVE' | 'TRIAL' | 'PENDING_APPROVAL' | 'PAST_DUE' | 'CANCELLED' | 'EXPIRED' | string;
  trialStart?: string | null;
  trialEnd?: string | null;
  currentPeriodStart?: string | null;
  currentPeriodEnd?: string | null;
  cancelAtPeriodEnd: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface PaymentTransaction {
  id: string;
  userId: number;
  subscriptionId: number;
  planCode: string;
  amountKes: number;
  phoneNumber: string;
  status: string;
  merchantRequestId?: string | null;
  checkoutRequestId?: string | null;
  responseDescription?: string | null;
  resultCode?: number | null;
  mpesaReceiptNumber?: string | null;
  initiatedAt: string;
  completedAt?: string | null;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  paymentId: string;
  userId: number;
  subscriptionId: number;
  amountKes: number;
  currency: string;
  issuedAt: string;
  mpesaReceiptNumber?: string | null;
}

export interface SubscriptionSnapshot {
  subscription: UserSubscription;
  plan: SubscriptionPlan;
  paymentTransactions: PaymentTransaction[];
  invoices: Invoice[];
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
