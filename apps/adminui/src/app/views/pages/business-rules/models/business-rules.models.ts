// Type definitions for Business Rules Management System
// Matches backend C# models from ADMIN_UI_UX_BUSINESS_RULES_PLAN.md

export type RuleType = 'MaxGroupSize' | 'MinAdvanceHours' | 'RestrictedHours' | 'CustomValidation' | 'MaxQuantity' | 'RequiresBooking';
export type AuditAction = 'CREATE' | 'UPDATE' | 'DELETE' | 'ACTIVATE' | 'DEACTIVATE';
export type EntityType = 'ServiceBusinessRule' | 'RequestItemRule' | 'UpsellItem';

export interface Service {
  id: number;
  tenantId: number;
  name: string;
  category: string;
  description?: string;
  isActive: boolean;
  ruleCount?: number;
  activeRuleCount?: number;
  lastModified?: Date;
  lastModifiedBy?: string;
  createdAt: Date;
  updatedAt?: Date;
}

export interface ServiceBusinessRule {
  id: number;
  tenantId: number;
  serviceId: number;
  ruleType: string;
  ruleKey: string;
  ruleValue: string;  // JSON string
  validationMessage?: string;
  priority: number;
  isActive: boolean;
  upsellSuggestions?: string;  // JSON array of UpsellItem IDs
  relevanceContext?: string;
  minConfidenceScore?: number;
  createdAt: Date;
  updatedAt?: Date;

  // Populated from joins
  service?: Service;
  tenant?: Tenant;
}

export interface RequestItem {
  id: number;
  tenantId: number;
  name: string;
  description?: string;
  category: string;
  department: string;
  stockQuantity?: number;
  isActive: boolean;
  ruleCount?: number;
  activeRuleCount?: number;
  lastModified?: Date;
  lastModifiedBy?: string;
  createdAt: Date;
  updatedAt?: Date;
}

export interface RequestItemRule {
  id: number;
  tenantId: number;
  requestItemId: number;
  ruleType: string;
  ruleKey: string;
  ruleValue: string;  // JSON string
  validationMessage?: string;
  priority: number;
  isActive: boolean;
  upsellSuggestions?: string;  // JSON array of UpsellItem IDs
  minConfidenceScore?: number;
  createdAt: Date;
  updatedAt?: Date;

  // Populated from joins
  requestItem?: RequestItem;
  tenant?: Tenant;
}

export interface UpsellItem {
  id: number;
  tenantId: number;
  title: string;
  description: string;
  priceCents: number;
  unit: string;
  categories: string[];  // Array of category strings
  isActive: boolean;
  imageUrl?: string;
  priority: number;
  leadTimeMinutes?: number;
  createdAt: Date;
  updatedAt?: Date;
}

export interface UpsellStrategy {
  id: number;
  tenantId: number;
  name: string;
  description: string;
  triggerType: 'Service' | 'RequestItem' | 'Context';
  triggerId?: number;
  upsellItemIds: number[];
  relevanceThreshold: number;
  isActive: boolean;
  createdAt: Date;
  updatedAt?: Date;
}

export interface UpsellAnalytics {
  totalRevenue: number;
  conversionRate: number;
  totalSuggestions: number;
  totalAccepted: number;
  topPerformers: UpsellPerformance[];
  trendData: UpsellTrendData[];
}

export interface UpsellPerformance {
  upsellItemId: number;
  upsellItemTitle: string;
  suggestions: number;
  acceptances: number;
  conversionRate: number;
  revenue: number;
}

export interface UpsellTrendData {
  date: Date;
  suggestions: number;
  acceptances: number;
  revenue: number;
}

export interface AuditLogEntry {
  id: number;
  tenantId: number;
  userId: number;
  userName: string;
  userEmail: string;
  action: AuditAction;
  entityType: EntityType;
  entityId: number;
  entityName?: string;
  changesBefore?: string;  // JSON
  changesAfter?: string;   // JSON
  notes?: string;
  ipAddress: string;
  userAgent: string;
  timestamp: Date;
}

export interface Tenant {
  id: number;
  name: string;
  subdomain: string;
}

// Request/Response DTOs
export interface CreateServiceRuleRequest {
  serviceId: number;
  ruleType: string;
  ruleKey: string;
  ruleValue: string;
  validationMessage: string;
  priority: number;
  isActive: boolean;
  upsellSuggestions?: number[];
  relevanceContext?: string;
  minConfidenceScore?: number;
}

export interface UpdateServiceRuleRequest {
  ruleType?: string;
  ruleKey?: string;
  ruleValue?: string;
  validationMessage?: string;
  priority?: number;
  isActive?: boolean;
  upsellSuggestions?: number[];
  relevanceContext?: string;
  minConfidenceScore?: number;
}

export interface CreateRequestItemRuleRequest {
  requestItemId: number;
  ruleType: string;
  ruleKey: string;
  ruleValue: string;
  validationMessage: string;
  priority: number;
  isActive: boolean;
  upsellSuggestions?: number[];
  minConfidenceScore?: number;
}

export interface UpdateRequestItemRuleRequest {
  ruleType?: string;
  ruleKey?: string;
  ruleValue?: string;
  validationMessage?: string;
  priority?: number;
  isActive?: boolean;
  upsellSuggestions?: number[];
  minConfidenceScore?: number;
}

export interface CreateUpsellItemRequest {
  title: string;
  description: string;
  priceCents: number;
  unit: string;
  categories: string[];
  isActive: boolean;
  imageUrl?: string;
  priority: number;
  leadTimeMinutes?: number;
}

export interface UpdateUpsellItemRequest {
  title?: string;
  description?: string;
  priceCents?: number;
  unit?: string;
  categories?: string[];
  isActive?: boolean;
  imageUrl?: string;
  priority?: number;
  leadTimeMinutes?: number;
}

// Filter interfaces
export interface BusinessRulesFilter {
  searchTerm?: string;
  category?: string;
  department?: string;
  isActive?: boolean;
  hasRules?: boolean;
  sortBy?: 'name' | 'category' | 'ruleCount' | 'lastModified';
  sortDirection?: 'asc' | 'desc';
}

export interface AuditLogFilter {
  searchTerm?: string;
  userId?: number;
  action?: AuditAction;
  entityType?: EntityType;
  dateFrom?: Date;
  dateTo?: Date;
  sortBy?: 'timestamp' | 'userName' | 'action';
  sortDirection?: 'asc' | 'desc';
}

// Dashboard statistics
export interface BusinessRulesStats {
  totalRules: number;
  activeRules: number;
  draftRules: number;
  inactiveRules: number;
  totalServices: number;
  servicesWithRules: number;
  totalRequestItems: number;
  requestItemsWithRules: number;
  totalUpsellItems: number;
  activeUpsellItems: number;
}

// Rule templates
export interface RuleTemplate {
  id: number;
  name: string;
  description: string;
  ruleType: string;
  category: string;
  defaultRuleValue: string;
  defaultValidationMessage: string;
  defaultPriority: number;
  isBuiltIn: boolean;
  usageCount: number;
  createdAt: Date;
}

// Weather Upselling models
export interface WeatherUpsellRule {
  id: number;
  tenantId: number;
  weatherCondition: string;  // hot, warm, mild, cold, rainy, stormy, cloudy
  minTemperature?: number;
  maxTemperature?: number;
  weatherCodes?: string;      // JSON array of WMO weather codes
  serviceIds: string;         // JSON array of service IDs
  bannerText?: string;
  bannerIcon?: string;
  priority: number;
  isActive: boolean;
  createdAt: Date;
  updatedAt: Date;
}

export interface WeatherConditionInfo {
  code: string;
  name: string;
  description: string;
  defaultMinTemp?: number;
  defaultMaxTemp?: number;
  defaultIcon: string;
  wmoCodes?: number[];
}

export interface ServiceForUpsell {
  id: number;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  imageUrl?: string;
  isChargeable: boolean;
  price: string;
  priceAmount?: number;
  currency?: string;
}

export interface CreateWeatherUpsellRuleRequest {
  weatherCondition: string;
  minTemperature?: number;
  maxTemperature?: number;
  weatherCodes?: string;
  serviceIds: string;
  bannerText?: string;
  bannerIcon?: string;
  priority: number;
  isActive: boolean;
}

export interface UpdateWeatherUpsellRuleRequest {
  weatherCondition: string;
  minTemperature?: number;
  maxTemperature?: number;
  weatherCodes?: string;
  serviceIds: string;
  bannerText?: string;
  bannerIcon?: string;
  priority: number;
  isActive: boolean;
}
