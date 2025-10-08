import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';
import {
  Service,
  ServiceBusinessRule,
  RequestItem,
  RequestItemRule,
  UpsellItem,
  UpsellStrategy,
  UpsellAnalytics,
  AuditLogEntry,
  BusinessRulesStats,
  BusinessRulesFilter,
  AuditLogFilter,
  CreateServiceRuleRequest,
  UpdateServiceRuleRequest,
  CreateRequestItemRuleRequest,
  UpdateRequestItemRuleRequest,
  CreateUpsellItemRequest,
  UpdateUpsellItemRequest,
  RuleTemplate
} from '../models/business-rules.models';

@Injectable({
  providedIn: 'root'
})
export class BusinessRulesService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl || 'http://localhost:5000/api';

  // Service Business Rules APIs
  getServices(tenantId: number, filter?: BusinessRulesFilter): Observable<Service[]> {
    let params = new HttpParams();
    if (filter) {
      if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
      if (filter.category) params = params.set('category', filter.category);
      if (filter.isActive !== undefined) params = params.set('isActive', filter.isActive.toString());
      if (filter.hasRules !== undefined) params = params.set('hasRules', filter.hasRules.toString());
      if (filter.sortBy) params = params.set('sortBy', filter.sortBy);
      if (filter.sortDirection) params = params.set('sortDirection', filter.sortDirection);
    }

    return this.http.get<Service[]>(`${this.baseUrl}/admin/services/${tenantId}`, { params })
      .pipe(
        map(services => services.map(s => ({
          ...s,
          createdAt: new Date(s.createdAt),
          updatedAt: s.updatedAt ? new Date(s.updatedAt) : undefined,
          lastModified: s.lastModified ? new Date(s.lastModified) : undefined
        }))),
        catchError(error => {
          console.error('Error loading services:', error);
          return this.getMockServices(tenantId);
        })
      );
  }

  getServiceById(tenantId: number, serviceId: number): Observable<Service> {
    return this.http.get<Service>(`${this.baseUrl}/admin/services/${tenantId}/${serviceId}`)
      .pipe(
        map(service => ({
          ...service,
          createdAt: new Date(service.createdAt),
          updatedAt: service.updatedAt ? new Date(service.updatedAt) : undefined,
          lastModified: service.lastModified ? new Date(service.lastModified) : undefined
        })),
        catchError(error => {
          console.error('Error loading service:', error);
          return of({} as Service);
        })
      );
  }

  getServiceRules(tenantId: number, serviceId: number): Observable<ServiceBusinessRule[]> {
    return this.http.get<ServiceBusinessRule[]>(`${this.baseUrl}/admin/services/${tenantId}/${serviceId}/rules`)
      .pipe(
        map(rules => rules.map(r => ({
          ...r,
          createdAt: new Date(r.createdAt),
          updatedAt: r.updatedAt ? new Date(r.updatedAt) : undefined
        }))),
        catchError(error => {
          console.error('Error loading service rules:', error);
          return this.getMockServiceRules(tenantId, serviceId);
        })
      );
  }

  createServiceRule(tenantId: number, serviceId: number, request: CreateServiceRuleRequest): Observable<ServiceBusinessRule> {
    return this.http.post<ServiceBusinessRule>(`${this.baseUrl}/admin/services/${tenantId}/${serviceId}/rules`, request)
      .pipe(
        map(rule => ({
          ...rule,
          createdAt: new Date(rule.createdAt),
          updatedAt: rule.updatedAt ? new Date(rule.updatedAt) : undefined
        })),
        catchError(error => {
          console.error('Error creating service rule:', error);
          throw error;
        })
      );
  }

  updateServiceRule(tenantId: number, serviceId: number, ruleId: number, request: UpdateServiceRuleRequest): Observable<ServiceBusinessRule> {
    return this.http.put<ServiceBusinessRule>(`${this.baseUrl}/admin/services/${tenantId}/${serviceId}/rules/${ruleId}`, request)
      .pipe(
        map(rule => ({
          ...rule,
          createdAt: new Date(rule.createdAt),
          updatedAt: rule.updatedAt ? new Date(rule.updatedAt) : undefined
        })),
        catchError(error => {
          console.error('Error updating service rule:', error);
          throw error;
        })
      );
  }

  deleteServiceRule(tenantId: number, serviceId: number, ruleId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/admin/services/${tenantId}/${serviceId}/rules/${ruleId}`)
      .pipe(
        catchError(error => {
          console.error('Error deleting service rule:', error);
          throw error;
        })
      );
  }

  toggleServiceRuleActive(tenantId: number, serviceId: number, ruleId: number, isActive: boolean): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/admin/services/${tenantId}/${serviceId}/rules/${ruleId}/toggle`, { isActive })
      .pipe(
        catchError(error => {
          console.error('Error toggling service rule:', error);
          throw error;
        })
      );
  }

  // Request Item Rules APIs
  getRequestItems(tenantId: number, filter?: BusinessRulesFilter): Observable<RequestItem[]> {
    let params = new HttpParams();
    if (filter) {
      if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
      if (filter.category) params = params.set('category', filter.category);
      if (filter.department) params = params.set('department', filter.department);
      if (filter.isActive !== undefined) params = params.set('isActive', filter.isActive.toString());
      if (filter.hasRules !== undefined) params = params.set('hasRules', filter.hasRules.toString());
      if (filter.sortBy) params = params.set('sortBy', filter.sortBy);
      if (filter.sortDirection) params = params.set('sortDirection', filter.sortDirection);
    }

    return this.http.get<RequestItem[]>(`${this.baseUrl}/admin/request-items/${tenantId}`, { params })
      .pipe(
        map(items => items.map(i => ({
          ...i,
          createdAt: new Date(i.createdAt),
          updatedAt: i.updatedAt ? new Date(i.updatedAt) : undefined,
          lastModified: i.lastModified ? new Date(i.lastModified) : undefined
        }))),
        catchError(error => {
          console.error('Error loading request items:', error);
          return this.getMockRequestItems(tenantId);
        })
      );
  }

  getRequestItemRules(tenantId: number, itemId: number): Observable<RequestItemRule[]> {
    return this.http.get<RequestItemRule[]>(`${this.baseUrl}/admin/request-items/${tenantId}/${itemId}/rules`)
      .pipe(
        map(rules => rules.map(r => ({
          ...r,
          createdAt: new Date(r.createdAt),
          updatedAt: r.updatedAt ? new Date(r.updatedAt) : undefined
        }))),
        catchError(error => {
          console.error('Error loading request item rules:', error);
          return this.getMockRequestItemRules(tenantId, itemId);
        })
      );
  }

  createRequestItemRule(tenantId: number, itemId: number, request: CreateRequestItemRuleRequest): Observable<RequestItemRule> {
    return this.http.post<RequestItemRule>(`${this.baseUrl}/admin/request-items/${tenantId}/${itemId}/rules`, request)
      .pipe(
        map(rule => ({
          ...rule,
          createdAt: new Date(rule.createdAt),
          updatedAt: rule.updatedAt ? new Date(rule.updatedAt) : undefined
        })),
        catchError(error => {
          console.error('Error creating request item rule:', error);
          throw error;
        })
      );
  }

  updateRequestItemRule(tenantId: number, itemId: number, ruleId: number, request: UpdateRequestItemRuleRequest): Observable<RequestItemRule> {
    return this.http.put<RequestItemRule>(`${this.baseUrl}/admin/request-items/${tenantId}/${itemId}/rules/${ruleId}`, request)
      .pipe(
        map(rule => ({
          ...rule,
          createdAt: new Date(rule.createdAt),
          updatedAt: rule.updatedAt ? new Date(rule.updatedAt) : undefined
        })),
        catchError(error => {
          console.error('Error updating request item rule:', error);
          throw error;
        })
      );
  }

  deleteRequestItemRule(tenantId: number, itemId: number, ruleId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/admin/request-items/${tenantId}/${itemId}/rules/${ruleId}`)
      .pipe(
        catchError(error => {
          console.error('Error deleting request item rule:', error);
          throw error;
        })
      );
  }

  // Upselling APIs
  getUpsellItems(tenantId: number): Observable<UpsellItem[]> {
    return this.http.get<UpsellItem[]>(`${this.baseUrl}/admin/upsell-items/${tenantId}`)
      .pipe(
        map(items => items.map(i => ({
          ...i,
          createdAt: new Date(i.createdAt),
          updatedAt: i.updatedAt ? new Date(i.updatedAt) : undefined
        }))),
        catchError(error => {
          console.error('Error loading upsell items:', error);
          return this.getMockUpsellItems(tenantId);
        })
      );
  }

  createUpsellItem(tenantId: number, request: CreateUpsellItemRequest): Observable<UpsellItem> {
    return this.http.post<UpsellItem>(`${this.baseUrl}/admin/upsell-items/${tenantId}`, request)
      .pipe(
        map(item => ({
          ...item,
          createdAt: new Date(item.createdAt),
          updatedAt: item.updatedAt ? new Date(item.updatedAt) : undefined
        })),
        catchError(error => {
          console.error('Error creating upsell item:', error);
          throw error;
        })
      );
  }

  updateUpsellItem(tenantId: number, itemId: number, request: UpdateUpsellItemRequest): Observable<UpsellItem> {
    return this.http.put<UpsellItem>(`${this.baseUrl}/admin/upsell-items/${tenantId}/${itemId}`, request)
      .pipe(
        map(item => ({
          ...item,
          createdAt: new Date(item.createdAt),
          updatedAt: item.updatedAt ? new Date(item.updatedAt) : undefined
        })),
        catchError(error => {
          console.error('Error updating upsell item:', error);
          throw error;
        })
      );
  }

  deleteUpsellItem(tenantId: number, itemId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/admin/upsell-items/${tenantId}/${itemId}`)
      .pipe(
        catchError(error => {
          console.error('Error deleting upsell item:', error);
          throw error;
        })
      );
  }

  getUpsellAnalytics(tenantId: number, days: number = 30): Observable<UpsellAnalytics> {
    return this.http.get<UpsellAnalytics>(`${this.baseUrl}/admin/upsell-analytics/${tenantId}?days=${days}`)
      .pipe(
        catchError(error => {
          console.error('Error loading upsell analytics:', error);
          return this.getMockUpsellAnalytics();
        })
      );
  }

  // Audit Log APIs
  getAuditLog(tenantId: number, filter?: AuditLogFilter): Observable<AuditLogEntry[]> {
    let params = new HttpParams();
    if (filter) {
      if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
      if (filter.userId) params = params.set('userId', filter.userId.toString());
      if (filter.action) params = params.set('action', filter.action);
      if (filter.entityType) params = params.set('entityType', filter.entityType);
      if (filter.dateFrom) params = params.set('dateFrom', filter.dateFrom.toISOString());
      if (filter.dateTo) params = params.set('dateTo', filter.dateTo.toISOString());
      if (filter.sortBy) params = params.set('sortBy', filter.sortBy);
      if (filter.sortDirection) params = params.set('sortDirection', filter.sortDirection);
    }

    return this.http.get<AuditLogEntry[]>(`${this.baseUrl}/admin/audit-log/${tenantId}`, { params })
      .pipe(
        map(entries => entries.map(e => ({
          ...e,
          timestamp: new Date(e.timestamp)
        }))),
        catchError(error => {
          console.error('Error loading audit log:', error);
          return this.getMockAuditLog(tenantId);
        })
      );
  }

  getAuditLogEntry(tenantId: number, entryId: number): Observable<AuditLogEntry> {
    return this.http.get<AuditLogEntry>(`${this.baseUrl}/admin/audit-log/${tenantId}/${entryId}`)
      .pipe(
        map(entry => ({
          ...entry,
          timestamp: new Date(entry.timestamp)
        })),
        catchError(error => {
          console.error('Error loading audit log entry:', error);
          return of({} as AuditLogEntry);
        })
      );
  }

  // Dashboard Statistics
  getBusinessRulesStats(tenantId: number): Observable<BusinessRulesStats> {
    return this.http.get<BusinessRulesStats>(`${this.baseUrl}/admin/business-rules/stats/${tenantId}`)
      .pipe(
        catchError(error => {
          console.error('Error loading business rules stats:', error);
          return this.getMockStats();
        })
      );
  }

  // Rule Templates
  getRuleTemplates(): Observable<RuleTemplate[]> {
    return this.http.get<RuleTemplate[]>(`${this.baseUrl}/admin/rule-templates`)
      .pipe(
        map(templates => templates.map(t => ({
          ...t,
          createdAt: new Date(t.createdAt)
        }))),
        catchError(error => {
          console.error('Error loading rule templates:', error);
          return of([]);
        })
      );
  }

  // Mock data methods for development (will be replaced when APIs are ready)
  private getMockServices(tenantId: number): Observable<Service[]> {
    const mockServices: Service[] = [
      {
        id: 1,
        tenantId,
        name: 'Spa Services',
        category: 'Wellness',
        description: 'Spa treatments and wellness services',
        isActive: true,
        ruleCount: 4,
        activeRuleCount: 3,
        lastModified: new Date(),
        lastModifiedBy: 'Sarah Johnson',
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: 2,
        tenantId,
        name: 'Room Service',
        category: 'Dining',
        description: 'In-room dining and beverage service',
        isActive: true,
        ruleCount: 2,
        activeRuleCount: 2,
        lastModified: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000),
        lastModifiedBy: 'James Smith',
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: 3,
        tenantId,
        name: 'Swimming Pool',
        category: 'Amenities',
        description: 'Pool access and amenities',
        isActive: true,
        ruleCount: 1,
        activeRuleCount: 1,
        lastModified: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000),
        lastModifiedBy: 'Maria Garcia',
        createdAt: new Date(),
        updatedAt: new Date()
      }
    ];
    return of(mockServices);
  }

  private getMockServiceRules(tenantId: number, serviceId: number): Observable<ServiceBusinessRule[]> {
    const mockRules: ServiceBusinessRule[] = [
      {
        id: 1,
        tenantId,
        serviceId,
        ruleType: 'MaxGroupSize',
        ruleKey: 'max_group_size',
        ruleValue: '{"maxPeople": 8}',
        validationMessage: 'I\'m sorry, but our spa services are limited to groups of 8 guests or fewer. For larger groups, please contact our concierge directly at ext. 123.',
        priority: 5,
        isActive: true,
        minConfidenceScore: 0.8,
        createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000),
        updatedAt: new Date()
      },
      {
        id: 2,
        tenantId,
        serviceId,
        ruleType: 'MinAdvanceHours',
        ruleKey: 'min_advance_booking',
        ruleValue: '{"minHours": 24}',
        validationMessage: 'Please note that spa bookings require at least 24 hours advance notice. Would you like me to check availability for tomorrow or later?',
        priority: 4,
        isActive: true,
        minConfidenceScore: 0.85,
        createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000),
        updatedAt: new Date()
      }
    ];
    return of(mockRules);
  }

  private getMockRequestItems(tenantId: number): Observable<RequestItem[]> {
    const mockItems: RequestItem[] = [
      {
        id: 1,
        tenantId,
        name: 'Extra Towels',
        description: 'Additional bath towels',
        category: 'Linens',
        department: 'Housekeeping',
        stockQuantity: 150,
        isActive: true,
        ruleCount: 2,
        activeRuleCount: 2,
        lastModified: new Date(),
        lastModifiedBy: 'Sarah Johnson',
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: 2,
        tenantId,
        name: 'Extra Pillows',
        description: 'Additional pillows',
        category: 'Linens',
        department: 'Housekeeping',
        stockQuantity: 80,
        isActive: true,
        ruleCount: 1,
        activeRuleCount: 1,
        lastModified: new Date(),
        lastModifiedBy: 'Maria Garcia',
        createdAt: new Date(),
        updatedAt: new Date()
      }
    ];
    return of(mockItems);
  }

  private getMockRequestItemRules(tenantId: number, itemId: number): Observable<RequestItemRule[]> {
    const mockRules: RequestItemRule[] = [
      {
        id: 1,
        tenantId,
        requestItemId: itemId,
        ruleType: 'MaxQuantity',
        ruleKey: 'max_quantity_per_room',
        ruleValue: '{"maxQuantity": 4}',
        validationMessage: 'We can provide up to 4 additional towels per room. Would you like me to send them to your room?',
        priority: 3,
        isActive: true,
        minConfidenceScore: 0.7,
        createdAt: new Date(),
        updatedAt: new Date()
      }
    ];
    return of(mockRules);
  }

  private getMockUpsellItems(tenantId: number): Observable<UpsellItem[]> {
    const mockItems: UpsellItem[] = [
      {
        id: 1,
        tenantId,
        title: 'Champagne Package',
        description: 'Premium champagne with chocolate-covered strawberries',
        priceCents: 8500,
        unit: 'package',
        categories: ['Beverage', 'Romance', 'Celebration'],
        isActive: true,
        priority: 5,
        leadTimeMinutes: 30,
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: 2,
        tenantId,
        title: 'Premium Bath Amenities',
        description: 'Luxury bath products and aromatherapy kit',
        priceCents: 3500,
        unit: 'set',
        categories: ['Wellness', 'Spa'],
        isActive: true,
        priority: 4,
        leadTimeMinutes: 15,
        createdAt: new Date(),
        updatedAt: new Date()
      }
    ];
    return of(mockItems);
  }

  private getMockUpsellAnalytics(): Observable<UpsellAnalytics> {
    const mockAnalytics: UpsellAnalytics = {
      totalRevenue: 124500,
      conversionRate: 18.5,
      totalSuggestions: 350,
      totalAccepted: 65,
      topPerformers: [
        {
          upsellItemId: 1,
          upsellItemTitle: 'Champagne Package',
          suggestions: 120,
          acceptances: 28,
          conversionRate: 23.3,
          revenue: 23800
        },
        {
          upsellItemId: 2,
          upsellItemTitle: 'Premium Bath Amenities',
          suggestions: 230,
          acceptances: 37,
          conversionRate: 16.1,
          revenue: 12950
        }
      ],
      trendData: []
    };
    return of(mockAnalytics);
  }

  private getMockAuditLog(tenantId: number): Observable<AuditLogEntry[]> {
    const mockEntries: AuditLogEntry[] = [
      {
        id: 1,
        tenantId,
        userId: 1,
        userName: 'Sarah Johnson',
        userEmail: 'sarah@hotel.com',
        action: 'CREATE',
        entityType: 'ServiceBusinessRule',
        entityId: 1,
        entityName: 'Spa Max Group Size',
        changesAfter: '{"ruleType":"MaxGroupSize","ruleValue":"{\\"maxPeople\\":8}"}',
        ipAddress: '192.168.1.100',
        userAgent: 'Mozilla/5.0',
        timestamp: new Date(Date.now() - 2 * 60 * 60 * 1000)
      },
      {
        id: 2,
        tenantId,
        userId: 2,
        userName: 'James Smith',
        userEmail: 'james@hotel.com',
        action: 'UPDATE',
        entityType: 'ServiceBusinessRule',
        entityId: 2,
        entityName: 'Room Service Hours',
        changesBefore: '{"isActive":false}',
        changesAfter: '{"isActive":true}',
        ipAddress: '192.168.1.101',
        userAgent: 'Mozilla/5.0',
        timestamp: new Date(Date.now() - 5 * 60 * 60 * 1000)
      }
    ];
    return of(mockEntries);
  }

  private getMockStats(): Observable<BusinessRulesStats> {
    const mockStats: BusinessRulesStats = {
      totalRules: 58,
      activeRules: 47,
      draftRules: 3,
      inactiveRules: 8,
      totalServices: 12,
      servicesWithRules: 8,
      totalRequestItems: 25,
      requestItemsWithRules: 15,
      totalUpsellItems: 18,
      activeUpsellItems: 15
    };
    return of(mockStats);
  }
}
