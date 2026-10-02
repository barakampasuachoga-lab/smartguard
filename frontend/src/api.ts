import axios from 'axios';
import type { AdminUserDetails, AlertItem, AuthUser, DashboardOverview, DeviceSummary, LoginRequest, LoginResponse, Property, PropertyCreateRequest, RegisterRequest, RegisterResponse, SecurityEvent, SystemSetting, ToggleUserBlockRequest, UpdateProfileRequest, UserReport } from './types';

const api = axios.create({
  baseURL: 'http://localhost:5156/api',
});

export const getProfilePhotoUrl = (path?: string | null) => path
  ? new URL(path, 'http://localhost:5156').toString()
  : null;

export const getPropertyPhotoUrl = (path?: string | null) => path
  ? new URL(path, 'http://localhost:5156').toString()
  : null;

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('smartguard-token');

  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
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

export const acknowledgeAlert = async (id: string) => {
  const { data } = await api.post(`/security/alerts/${id}/acknowledge`);
  return data;
};

export const resolveAlert = async (id: string) => {
  const { data } = await api.post<AlertItem>(`/security/alerts/${id}/resolve`);
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
