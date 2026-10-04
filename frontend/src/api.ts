import axios from 'axios';
import type { AdminUserDetails, AlertItem, AuthUser, DashboardOverview, DeviceSummary, Invoice, LoginRequest, LoginResponse, PaymentTransaction, Property, PropertyCreateRequest, RegisterRequest, RegisterResponse, SecurityCheckIn, SecurityEvent, SecuritySchedule, SubscriptionPlan, SubscriptionSnapshot, SystemSetting, ToggleUserBlockRequest, TrustedContact, UpdateProfileRequest, UserReport, UserSubscription } from './types';

const api = axios.create({
  baseURL: '/api',
});

const getAssetBaseUrl = () => {
  const { hostname, origin } = window.location;
  return hostname === 'localhost' || hostname === '127.0.0.1' ? 'http://localhost:5156' : origin;
};

export const getProfilePhotoUrl = (path?: string | null) => path
  ? new URL(path, getAssetBaseUrl()).toString()
  : null;

export const getPropertyPhotoUrl = (path?: string | null) => path
  ? new URL(path, getAssetBaseUrl()).toString()
  : null;

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('smartguard-token');

  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

api.interceptors.response.use(response => response, error => {
  if (error?.response?.status === 401 && localStorage.getItem('smartguard-token')) {
    clearStoredSession();
    if (window.location.pathname !== '/login') window.location.assign('/login');
  }
  return Promise.reject(error);
});

export const getStoredAuthToken = () => localStorage.getItem('smartguard-token');

export const clearStoredSession = () => {
  localStorage.removeItem('smartguard-token');
  localStorage.removeItem('smartguard-user');
};

export const loginUser = async (payload: LoginRequest): Promise<LoginResponse> => {
  const { data } = await api.post('/auth/login', payload);
  return data as LoginResponse;
};

export const requestPasswordReset = async (email: string): Promise<{ message: string }> => {
  const { data } = await api.post<{ message: string }>('/auth/forgot-password', { email });
  return data;
};

export const resetPassword = async (token: string, newPassword: string): Promise<{ message: string }> => {
  const { data } = await api.post<{ message: string }>('/auth/reset-password', { token, newPassword });
  return data;
};

export const getSubscriptionPlans = async (): Promise<SubscriptionPlan[]> => {
  const { data } = await api.get<SubscriptionPlan[]>('/subscription/plans');
  return data;
};

export const getMySubscription = async (): Promise<SubscriptionSnapshot> => {
  const { data } = await api.get<SubscriptionSnapshot>('/subscription/me');
  return data;
};

export const startSubscriptionCheckout = async (planCode: string, phoneNumber: string): Promise<{ id: string; status: string; plan: string; amountKes: number; phoneNumber: string; message: string }> => {
  const { data } = await api.post('/subscription/checkout', { planCode, phoneNumber });
  return data;
};

export const getPaymentTransaction = async (id: string): Promise<PaymentTransaction> => {
  const { data } = await api.get<PaymentTransaction>(`/subscription/transactions/${id}`);
  return data;
};

export const cancelSubscription = async (): Promise<{ message: string; subscription: UserSubscription }> => {
  const { data } = await api.post('/subscription/cancel');
  return data;
};

export interface AdminBillingSummary {
  revenueThisMonthKes: number;
  totalRevenueKes: number;
  activeSubscriptions: number;
  trialSubscriptions: number;
  pastDueSubscriptions: number;
  subscriptions: Array<{ id: number; userId: number; userName?: string | null; email?: string | null; plan?: string | null; status: string; trialEnd?: string | null; currentPeriodEnd?: string | null; cancelAtPeriodEnd: boolean }>;
  latestPayments: Array<{ id: string; userId: number; amountKes: number; mpesaReceiptNumber: string; paidAt: string }>;
}

export interface AdminBillingPayment {
  payment: { id: string; userId: number; subscriptionId: number; amountKes: number; mpesaReceiptNumber: string; paidAt: string };
  user?: { fullName: string; email: string };
}

export const getAdminBillingSummary = async (): Promise<AdminBillingSummary> => {
  const { data } = await api.get<AdminBillingSummary>('/subscription/admin/summary');
  return data;
};

export const getAdminBillingPayments = async (): Promise<AdminBillingPayment[]> => {
  const { data } = await api.get('/subscription/admin/payments');
  return data;
};

export const registerUser = async (payload: RegisterRequest, profilePhoto: File): Promise<RegisterResponse> => {
  const form = new FormData();
  form.append('fullName', payload.fullName);
  form.append('email', payload.email);
  form.append('password', payload.password);
  form.append('profilePhoto', profilePhoto);
  const { data } = await api.post('/auth/register', form);
  return data as RegisterResponse;
};

export const getProfile = async (): Promise<AuthUser> => {
  const { data } = await api.get<AuthUser>('/auth/me');
  return data;
};

export const updateProfile = async (payload: UpdateProfileRequest): Promise<LoginResponse> => {
  const { data } = await api.put<LoginResponse>('/auth/me', payload);
  return data;
};

export const updateProfilePhoto = async (profilePhoto: File): Promise<AuthUser> => {
  const form = new FormData();
  form.append('profilePhoto', profilePhoto);
  const { data } = await api.post<AuthUser>('/auth/me/photo', form);
  return data;
};

export const getUsers = async (): Promise<AuthUser[]> => {
  const { data } = await api.get<AuthUser[]>('/auth/users');
  return data;
};

export const getAdminUserDetails = async (id: number): Promise<AdminUserDetails> => {
  const { data } = await api.get<AdminUserDetails>(`/auth/users/${id}/details`);
  return data;
};

export const approveAdminSubscriptionPayment = async (transactionId: string): Promise<{ message: string; subscription: UserSubscription }> => {
  const { data } = await api.post<{ message: string; subscription: UserSubscription }>(`/subscription/admin/transactions/${transactionId}/approve`);
  return data;
};

export const sendUserReport = async (id: number, payload: { title: string; body: string }): Promise<UserReport> => {
  const { data } = await api.post<UserReport>(`/auth/users/${id}/reports`, payload);
  return data;
};

export const sendUserEmail = async (id: number, payload: { subject: string; body: string }): Promise<{ message: string }> => {
  const { data } = await api.post<{ message: string }>(`/auth/users/${id}/email`, payload);
  return data;
};

export const getMyReports = async (): Promise<UserReport[]> => {
  const { data } = await api.get<UserReport[]>('/auth/me/reports');
  return data;
};

export const downloadUserReport = (report: UserReport) => {
  const contents = [
    report.title,
    'From: SmartGuard administration',
    `Date: ${new Date(report.createdAt).toLocaleString()}`,
    '',
    report.body,
  ].join('\r\n');
  const safeTitle = report.title
    .replace(/[<>:"/\\|?*\u0000-\u001F]/g, '-')
    .trim()
    .replace(/\s+/g, '-')
    .slice(0, 80) || 'smartguard-report';
  const file = new Blob([contents], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(file);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${safeTitle}-${report.id}.txt`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
};

export const toggleUserBlock = async (id: number, payload: ToggleUserBlockRequest) => {
  const { data } = await api.patch(`/auth/users/${id}/block`, payload);
  return data;
};

export const getOverview = async (): Promise<DashboardOverview> => {
  const { data } = await api.get<DashboardOverview>('/security/overview');
  return data;
};

export const getProperties = async (): Promise<Property[]> => {
  const { data } = await api.get<Property[]>('/security/properties');
  return data;
};

const toPropertyForm = (payload: PropertyCreateRequest, propertyPhoto?: File | null) => {
  const form = new FormData();
  form.append('name', payload.name);
  form.append('address', payload.address);
  form.append('owner', payload.owner);
  form.append('status', payload.status);
  if (payload.ownerUserId !== undefined) form.append('ownerUserId', String(payload.ownerUserId));
  if (propertyPhoto) form.append('propertyPhoto', propertyPhoto);
  return form;
};

export const createProperty = async (payload: PropertyCreateRequest, propertyPhoto: File): Promise<Property> => {
  const { data } = await api.post<Property>('/security/properties', toPropertyForm(payload, propertyPhoto));
  return data;
};

export const updateProperty = async (id: string, payload: PropertyCreateRequest, propertyPhoto?: File | null): Promise<Property> => {
  const { data } = await api.put<Property>(`/security/properties/${encodeURIComponent(id)}`, toPropertyForm(payload, propertyPhoto));
  return data;
};

export const deleteProperty = async (id: string): Promise<void> => {
  await api.delete(`/security/properties/${encodeURIComponent(id)}`);
};

export const getEvents = async (): Promise<SecurityEvent[]> => {
  const { data } = await api.get<SecurityEvent[]>('/security/events');
  return data;
};

export const reviewEvent = async (id: string): Promise<SecurityEvent> => {
  const { data } = await api.post<SecurityEvent>(`/security/events/${id}/review`);
  return data;
};

export const getDevices = async (): Promise<DeviceSummary[]> => {
  const { data } = await api.get<DeviceSummary[]>('/security/devices');
  return data;
};

export const getAlerts = async (): Promise<AlertItem[]> => {
  const { data } = await api.get<AlertItem[]>('/security/alerts');
  return data;
};

export const getAlert = async (id: string): Promise<AlertItem> => {
  const { data } = await api.get<AlertItem>(`/security/alerts/${id}`);
  return data;
};

export const downloadSecurityReport = async () => {
  const response = await api.get('/security/reports', { responseType: 'blob' });
  const url = window.URL.createObjectURL(new Blob([response.data]));
  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', 'smartguard-security-report.csv');
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
};

export const acknowledgeAlert = async (id: string, comment?: string) => {
  const { data } = await api.post<AlertItem>(`/security/alerts/${id}/acknowledge`, { comment });
  return data;
};

export const resolveAlert = async (id: string, resolutionNote?: string) => {
  const { data } = await api.post<AlertItem>(`/security/alerts/${id}/resolve`, { resolutionNote });
  return data;
};

export const getSecuritySchedule = async (id: string): Promise<SecuritySchedule> => {
  const { data } = await api.get<SecuritySchedule>(`/security/properties/${encodeURIComponent(id)}/schedule`);
  return data;
};

export const updateSecuritySchedule = async (id: string, schedule: SecuritySchedule): Promise<SecuritySchedule> => {
  const { data } = await api.put<SecuritySchedule>(`/security/properties/${encodeURIComponent(id)}/schedule`, schedule);
  return data;
};

export const getTrustedContacts = async (propertyId: string): Promise<TrustedContact[]> => {
  const { data } = await api.get<TrustedContact[]>(`/security/properties/${encodeURIComponent(propertyId)}/trusted-contacts`);
  return data;
};

export const addTrustedContact = async (propertyId: string, contact: Omit<TrustedContact, 'id' | 'propertyId'>): Promise<TrustedContact> => {
  const { data } = await api.post<TrustedContact>(`/security/properties/${encodeURIComponent(propertyId)}/trusted-contacts`, contact);
  return data;
};

export const removeTrustedContact = async (propertyId: string, contactId: number): Promise<void> => {
  await api.delete(`/security/properties/${encodeURIComponent(propertyId)}/trusted-contacts/${contactId}`);
};

export const getSecurityCheckIns = async (propertyId: string): Promise<SecurityCheckIn[]> => {
  const { data } = await api.get<SecurityCheckIn[]>(`/security/properties/${encodeURIComponent(propertyId)}/check-ins`);
  return data;
};

export const createSecurityCheckIn = async (propertyId: string, note?: string): Promise<SecurityCheckIn> => {
  const { data } = await api.post<SecurityCheckIn>(`/security/properties/${encodeURIComponent(propertyId)}/check-ins`, { note });
  return data;
};

export const getSettings = async (): Promise<SystemSetting[]> => {
  const { data } = await api.get<SystemSetting[]>('/settings');
  return data;
};

export const updateSetting = async (name: string, value: string): Promise<SystemSetting> => {
  const { data } = await api.put<SystemSetting>(`/settings/${encodeURIComponent(name)}`, { value });
  return data;
};
