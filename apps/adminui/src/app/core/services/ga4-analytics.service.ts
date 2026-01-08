import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface GA4Overview {
  sessions: number;
  activeUsers: number;
  pageViews: number;
  engagementRate: number;
  avgSessionDuration: number;
  bounceRate: number;
  sessionsChange: number;
  usersChange: number;
  pageViewsChange: number;
}

export interface PageView {
  pagePath: string;
  pageTitle: string;
  views: number;
  uniqueViews: number;
  avgTimeOnPage: number;
}

export interface PageViewTimeSeries {
  date: string;
  pageViews: number;
  sessions: number;
  users: number;
}

export interface GA4Event {
  eventName: string;
  count: number;
  uniqueUsers: number;
}

export interface Engagement {
  avgSessionDuration: number;
  engagementRate: number;
  bounceRate: number;
  engagedSessions: number;
  avgPagesPerSession: number;
}

export interface GA4Status {
  configured: boolean;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class GA4AnalyticsService {
  private http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/ga4`;

  /**
   * Check if GA4 is configured
   */
  getStatus(): Observable<GA4Status> {
    return this.http.get<GA4Status>(`${this.apiUrl}/status`);
  }

  /**
   * Get analytics overview metrics
   */
  getOverview(startDate: string = '7daysAgo', endDate: string = 'today'): Observable<GA4Overview> {
    const params = new HttpParams()
      .set('startDate', startDate)
      .set('endDate', endDate);
    return this.http.get<GA4Overview>(`${this.apiUrl}/overview`, { params });
  }

  /**
   * Get top pages by views
   */
  getTopPages(startDate: string = '7daysAgo', endDate: string = 'today', limit: number = 10): Observable<PageView[]> {
    const params = new HttpParams()
      .set('startDate', startDate)
      .set('endDate', endDate)
      .set('limit', limit.toString());
    return this.http.get<PageView[]>(`${this.apiUrl}/top-pages`, { params });
  }

  /**
   * Get page views over time for charting
   */
  getPageViews(startDate: string = '7daysAgo', endDate: string = 'today'): Observable<PageViewTimeSeries[]> {
    const params = new HttpParams()
      .set('startDate', startDate)
      .set('endDate', endDate);
    return this.http.get<PageViewTimeSeries[]>(`${this.apiUrl}/page-views`, { params });
  }

  /**
   * Get custom events breakdown
   */
  getEvents(startDate: string = '7daysAgo', endDate: string = 'today'): Observable<GA4Event[]> {
    const params = new HttpParams()
      .set('startDate', startDate)
      .set('endDate', endDate);
    return this.http.get<GA4Event[]>(`${this.apiUrl}/events`, { params });
  }

  /**
   * Get user engagement metrics
   */
  getEngagement(startDate: string = '7daysAgo', endDate: string = 'today'): Observable<Engagement> {
    const params = new HttpParams()
      .set('startDate', startDate)
      .set('endDate', endDate);
    return this.http.get<Engagement>(`${this.apiUrl}/engagement`, { params });
  }
}
