export interface Tenant {
  id: number;
  name: string;
  slug: string;
  plan: 'Basic' | 'Standard' | 'Premium';
  timezone: string;
  themePrimary: string;
  status: 'Active' | 'Inactive' | 'Suspended';
  retentionDays: number;
  createdAt: Date;
}

export interface TenantSettings {
  tenantId: number;
  businessInfo?: BusinessInfo;
  whatsAppConfig?: WhatsAppConfig;
  emergencySettings?: EmergencySettings;
}

export interface BusinessInfo {
  id: number;
  tenantId: number;
  category: string;
  content: string;
  tags: string[];
  displayOrder: number;
  isActive: boolean;
}

export interface WhatsAppConfig {
  id: number;
  tenantId: number;
  wabaId: string;
  phoneNumberId: string;
  pageAccessToken: string;
  status: 'Active' | 'Inactive';
  createdAt: Date;
}

export interface EmergencySettings {
  autoEscalate: boolean;
  contactEmergencyServices: boolean;
  emergencyContacts: string[];
}