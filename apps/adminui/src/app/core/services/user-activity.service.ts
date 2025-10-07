import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface UserActivity {
  id: number;
  tenantId: number;
  actorUserId?: number;
  actorUserEmail: string;
  action: string;
  entity: string;
  entityId?: number;
  details?: string;
  createdAt: Date;
  timeAgo: string;
}

export interface ActivityFilter {
  searchTerm?: string;
  action?: string;
  entity?: string;
  userId?: number;
  dateFrom?: Date;
  dateTo?: Date;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

export interface ActivityStats {
  totalActivities: number;
  totalActiveUsers: number;
  todayActivities: number;
  weekActivities: number;
  activitiesByAction: { [key: string]: number };
  activitiesByEntity: { [key: string]: number };
  topActiveUsers: TopUserActivity[];
  recentActivitySummary: RecentActivitySummary[];
}

export interface TopUserActivity {
  userId: number;
  email: string;
  role: string;
  activityCount: number;
  lastActiveAt: Date;
}

export interface RecentActivitySummary {
  action: string;
  entity: string;
  count: number;
  lastOccurred: Date;
}

export interface ActivityResponse {
  activities: UserActivity[];
  totalCount: number;
  totalPages: number;
  currentPage: number;
  pageSize: number;
}

@Injectable({
  providedIn: 'root'
})
export class UserActivityService {
  private http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/useractivity`;

  /**
   * Get user activities with optional filtering
   */
  getActivities(filter?: ActivityFilter): Observable<ActivityResponse> {
    let params = new HttpParams();

    if (filter) {
      if (filter.searchTerm) {
        params = params.set('searchTerm', filter.searchTerm);
      }
      if (filter.action) {
        params = params.set('action', filter.action);
      }
      if (filter.entity) {
        params = params.set('entity', filter.entity);
      }
      if (filter.userId) {
        params = params.set('userId', filter.userId.toString());
      }
      if (filter.dateFrom) {
        params = params.set('dateFrom', filter.dateFrom.toISOString());
      }
      if (filter.dateTo) {
        params = params.set('dateTo', filter.dateTo.toISOString());
      }
      if (filter.page) {
        params = params.set('page', filter.page.toString());
      }
      if (filter.pageSize) {
        params = params.set('pageSize', filter.pageSize.toString());
      }
      if (filter.sortBy) {
        params = params.set('sortBy', filter.sortBy);
      }
      if (filter.sortDirection) {
        params = params.set('sortDirection', filter.sortDirection);
      }
    }

    return this.http.get<ActivityResponse>(this.apiUrl, { params })
      .pipe(
        catchError(error => {
          console.warn('User activity API not available:', error);
          return of({
            activities: [],
            totalCount: 0,
            totalPages: 0,
            currentPage: 1,
            pageSize: 20
          } as ActivityResponse);
        })
      );
  }

  /**
   * Get activity statistics
   */
  getActivityStats(): Observable<ActivityStats> {
    return this.http.get<ActivityStats>(`${this.apiUrl}/stats`)
      .pipe(
        catchError(error => {
          console.warn('User activity stats API not available:', error);
          return of({
            totalActivities: 0,
            totalActiveUsers: 0,
            todayActivities: 0,
            weekActivities: 0,
            activitiesByAction: {},
            activitiesByEntity: {},
            topActiveUsers: [],
            recentActivitySummary: []
          } as ActivityStats);
        })
      );
  }

  /**
   * Get activities for a specific user
   */
  getUserActivities(userId: number, filter?: ActivityFilter): Observable<ActivityResponse> {
    let params = new HttpParams();

    if (filter) {
      if (filter.searchTerm) {
        params = params.set('searchTerm', filter.searchTerm);
      }
      if (filter.action) {
        params = params.set('action', filter.action);
      }
      if (filter.entity) {
        params = params.set('entity', filter.entity);
      }
      if (filter.dateFrom) {
        params = params.set('dateFrom', filter.dateFrom.toISOString());
      }
      if (filter.dateTo) {
        params = params.set('dateTo', filter.dateTo.toISOString());
      }
      if (filter.page) {
        params = params.set('page', filter.page.toString());
      }
      if (filter.pageSize) {
        params = params.set('pageSize', filter.pageSize.toString());
      }
      if (filter.sortBy) {
        params = params.set('sortBy', filter.sortBy);
      }
      if (filter.sortDirection) {
        params = params.set('sortDirection', filter.sortDirection);
      }
    }

    return this.http.get<ActivityResponse>(`${this.apiUrl}/${userId}`, { params })
      .pipe(
        catchError(error => {
          console.warn('User activity API not available:', error);
          return of({
            activities: [],
            totalCount: 0,
            totalPages: 0,
            currentPage: 1,
            pageSize: 20
          } as ActivityResponse);
        })
      );
  }

  /**
   * Get available actions for filtering
   */
  getAvailableActions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/actions`)
      .pipe(
        catchError(error => {
          console.warn('User activity actions API not available:', error);
          return of(['create', 'update', 'delete', 'view', 'login', 'logout']);
        })
      );
  }

  /**
   * Get available entities for filtering
   */
  getAvailableEntities(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/entities`)
      .pipe(
        catchError(error => {
          console.warn('User activity entities API not available:', error);
          return of(['booking', 'task', 'message', 'conversation', 'user', 'tenant']);
        })
      );
  }

  /**
   * Format activity description for display
   */
  getActivityDescription(activity: UserActivity): string {
    const entity = activity.entity.toLowerCase();
    const action = activity.action.toLowerCase();

    switch (action) {
      case 'create':
        return `Created ${entity}${activity.entityId ? ` #${activity.entityId}` : ''}`;
      case 'update':
        return `Updated ${entity}${activity.entityId ? ` #${activity.entityId}` : ''}`;
      case 'delete':
        return `Deleted ${entity}${activity.entityId ? ` #${activity.entityId}` : ''}`;
      case 'view':
        return `Viewed ${entity}${activity.entityId ? ` #${activity.entityId}` : ''}`;
      case 'login':
        return 'Logged in to the system';
      case 'logout':
        return 'Logged out of the system';
      default:
        return `${action} ${entity}${activity.entityId ? ` #${activity.entityId}` : ''}`;
    }
  }

  /**
   * Get activity icon based on action
   */
  getActivityIcon(action: string): string {
    switch (action.toLowerCase()) {
      case 'create':
        return 'plus-circle';
      case 'update':
        return 'edit';
      case 'delete':
        return 'trash-2';
      case 'view':
        return 'eye';
      case 'login':
        return 'log-in';
      case 'logout':
        return 'log-out';
      case 'assign':
        return 'user-plus';
      case 'complete':
        return 'check-circle';
      case 'approve':
        return 'check';
      case 'reject':
        return 'x';
      default:
        return 'activity';
    }
  }

  /**
   * Get activity color based on action
   */
  getActivityColor(action: string): string {
    switch (action.toLowerCase()) {
      case 'create':
        return 'text-success';
      case 'update':
        return 'text-info';
      case 'delete':
        return 'text-danger';
      case 'view':
        return 'text-secondary';
      case 'login':
        return 'text-primary';
      case 'logout':
        return 'text-muted';
      case 'assign':
        return 'text-warning';
      case 'complete':
        return 'text-success';
      case 'approve':
        return 'text-success';
      case 'reject':
        return 'text-danger';
      default:
        return 'text-muted';
    }
  }

  /**
   * Format entity name for display
   */
  formatEntityName(entity: string): string {
    return entity.charAt(0).toUpperCase() + entity.slice(1).toLowerCase();
  }

  /**
   * Format action name for display
   */
  formatActionName(action: string): string {
    return action.charAt(0).toUpperCase() + action.slice(1).toLowerCase();
  }

  /**
   * Get entity icon
   */
  getEntityIcon(entity: string): string {
    switch (entity.toLowerCase()) {
      case 'booking':
        return 'calendar';
      case 'task':
        return 'clipboard';
      case 'stafftask':
        return 'clipboard';
      case 'message':
        return 'message-circle';
      case 'conversation':
        return 'message-square';
      case 'user':
        return 'user';
      case 'tenant':
        return 'building';
      case 'emergency':
        return 'alert-triangle';
      case 'incident':
        return 'alert-triangle';
      case 'broadcast':
        return 'radio';
      case 'notification':
        return 'bell';
      default:
        return 'file-text';
    }
  }

  /**
   * Create filter for date range
   */
  createDateRangeFilter(days: number): ActivityFilter {
    const now = new Date();
    const dateFrom = new Date(now);
    dateFrom.setDate(dateFrom.getDate() - days);

    return {
      dateFrom,
      dateTo: now,
      page: 1,
      pageSize: 20,
      sortBy: 'CreatedAt',
      sortDirection: 'desc'
    };
  }

  /**
   * Create filter for specific user
   */
  createUserFilter(userId: number): ActivityFilter {
    return {
      userId,
      page: 1,
      pageSize: 20,
      sortBy: 'CreatedAt',
      sortDirection: 'desc'
    };
  }

  /**
   * Create filter for specific action
   */
  createActionFilter(action: string): ActivityFilter {
    return {
      action,
      page: 1,
      pageSize: 20,
      sortBy: 'CreatedAt',
      sortDirection: 'desc'
    };
  }

  /**
   * Create filter for specific entity
   */
  createEntityFilter(entity: string): ActivityFilter {
    return {
      entity,
      page: 1,
      pageSize: 20,
      sortBy: 'CreatedAt',
      sortDirection: 'desc'
    };
  }

  /**
   * Merge filters
   */
  mergeFilters(...filters: (ActivityFilter | undefined)[]): ActivityFilter {
    return filters.reduce((merged: ActivityFilter, filter) => {
      if (!filter) return merged;
      return { ...merged, ...filter };
    }, {} as ActivityFilter);
  }
}