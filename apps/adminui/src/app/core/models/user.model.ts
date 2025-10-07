export interface User {
  id: number;
  email: string;
  userName: string;
  phoneNumber?: string;
  isActive: boolean;
  createdAt: Date;
  tenants?: UserTenant[];
}

export interface UserTenant {
  userId: number;
  tenantId: number;
  role: UserRole;
  createdAt: Date;
  tenant?: Tenant;
}

export type UserRole = 'Owner' | 'Manager' | 'FrontDesk' | 'Housekeeping' | 'Maintenance' | 'Concierge';

export interface LoginRequest {
  email: string;
  password: string;
  tenantSlug?: string;
}

export interface LoginResponse {
  success: boolean;
  token: string;
  refreshToken?: string;
  user: User;
  tenant: Tenant;
  requiresPasswordChange?: boolean;
  expiresIn?: number;
}

export interface AuthToken {
  token: string;
  refreshToken: string;
  expiresAt: Date;
}

export interface Tenant {
  id: number;
  name: string;
  slug: string;
}