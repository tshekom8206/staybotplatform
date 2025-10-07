import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { HttpParams } from '@angular/common/http';
import { ApiService, ApiResponse } from '../../../core/services/api.service';
import { DashboardStats } from './dashboard.component';

export interface RecentActivity {
  id: number;
  type: 'task_created' | 'task_completed' | 'guest_message' | 'emergency' | 'checkin' | 'checkout';
  title: string;
  description: string;
  timestamp: Date;
  icon: string;
  color: string;
}

export interface DepartmentTaskCount {
  department: string;
  count: number;
  color: string;
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {

  constructor(private apiService: ApiService) {}

  /**
   * Get dashboard statistics
   */
  getDashboardStats(date?: Date): Observable<DashboardStats> {
    let params = new HttpParams();
    if (date) {
      params = params.set('date', date.toISOString().split('T')[0]);
    }
    return this.apiService.get<ApiResponse<DashboardStats>>('/dashboard/stats', params)
      .pipe(
        map(response => response.data || {
          activeGuests: 0,
          pendingTasks: 0,
          todayCheckins: 0,
          todayCheckouts: 0,
          emergencyIncidents: 0,
          averageResponseTime: 0
        })
      );
  }

  /**
   * Get recent activities
   */
  getRecentActivities(limit: number = 10): Observable<RecentActivity[]> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.apiService.get<ApiResponse<RecentActivity[]>>('/dashboard/activities', params)
      .pipe(
        map(response => response.data || [])
      );
  }

  /**
   * Get task distribution by department
   */
  getDepartmentTasks(date?: Date): Observable<DepartmentTaskCount[]> {
    let params = new HttpParams();
    if (date) {
      params = params.set('date', date.toISOString().split('T')[0]);
    }
    return this.apiService.get<ApiResponse<DepartmentTaskCount[]>>('/dashboard/department-tasks', params)
      .pipe(
        map(response => response.data || [])
      );
  }

  /**
   * Get hourly activity data for charts
   */
  getHourlyActivity(date?: Date, period?: string): Observable<any> {
    let params = new HttpParams();
    if (date) {
      params = params.set('date', date.toISOString().split('T')[0]);
    }
    if (period) {
      params = params.set('period', period);
    }
    return this.apiService.get<ApiResponse<any>>('/dashboard/hourly-activity', params)
      .pipe(
        map(response => response.data || new Array(24).fill(0))
      );
  }

  /**
   * Get task completion trends (last 7 days)
   */
  getTaskCompletionTrend(endDate?: Date, days: number = 7): Observable<{ completed: number[], created: number[] }> {
    let params = new HttpParams().set('days', days.toString());
    if (endDate) {
      params = params.set('endDate', endDate.toISOString().split('T')[0]);
    }

    return this.apiService.get<ApiResponse<{ completed: number[], created: number[] }>>('/dashboard/task-completion-trend', params)
      .pipe(
        map(response => response.data || { completed: [], created: [] })
      );
  }

  /**
   * Get guest satisfaction score trend
   */
  getGuestSatisfactionTrend(): Observable<number[]> {
    return this.apiService.get<ApiResponse<number[]>>('/dashboard/satisfaction-trend')
      .pipe(
        map(response => response.data || [])
      );
  }

  /**
   * Get room occupancy percentage
   */
  getRoomOccupancy(date?: Date): Observable<{ occupancyPercentage: number, totalRooms: number, occupiedRooms: number, changeFromLastMonth: number }> {
    let params = new HttpParams();
    if (date) {
      params = params.set('date', date.toISOString().split('T')[0]);
    }
    return this.apiService.get<ApiResponse<{ occupancyPercentage: number, totalRooms: number, occupiedRooms: number, changeFromLastMonth: number }>>('/dashboard/room-occupancy', params)
      .pipe(
        map(response => response.data || { occupancyPercentage: 0, totalRooms: 0, occupiedRooms: 0, changeFromLastMonth: 0 })
      );
  }

  /**
   * Get completed tasks summary
   */
  getCompletedTasksSummary(date?: Date): Observable<{ completedToday: number, totalToday: number, completionRate: number, changeFromLastMonth: number }> {
    let params = new HttpParams();
    if (date) {
      params = params.set('date', date.toISOString().split('T')[0]);
    }
    return this.apiService.get<ApiResponse<{ completedToday: number, totalToday: number, completionRate: number, changeFromLastMonth: number }>>('/dashboard/completed-tasks-summary', params)
      .pipe(
        map(response => response.data || { completedToday: 0, totalToday: 0, completionRate: 0, changeFromLastMonth: 0 })
      );
  }

  /**
   * Get guest satisfaction rating
   */
  getGuestSatisfaction(startDate?: Date, endDate?: Date): Observable<{ averageRating: number, totalRatings: number, changeFromLastMonth: number, ratingDistribution: { [key: number]: number } }> {
    let params = new HttpParams();
    if (startDate) {
      params = params.set('startDate', startDate.toISOString().split('T')[0]);
    }
    if (endDate) {
      params = params.set('endDate', endDate.toISOString().split('T')[0]);
    }
    return this.apiService.get<ApiResponse<{ averageRating: number, totalRatings: number, changeFromLastMonth: number, ratingDistribution: { [key: number]: number } }>>('/dashboard/guest-satisfaction', params)
      .pipe(
        map(response => response.data || { averageRating: 0, totalRatings: 0, changeFromLastMonth: 0, ratingDistribution: {} })
      );
  }
}