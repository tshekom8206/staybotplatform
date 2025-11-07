import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgbDropdownModule, NgbProgressbarModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../../core/services/auth.service';

interface TaskPerformanceData {
  totalTasks: number;
  completedTasks: number;
  pendingTasks: number;
  inProgressTasks: number;
  overdueTasks: number;
  completionRate: number;
  averageCompletionHours: number;
  tasksByDepartment: { [key: string]: number };
  tasksByPriority: { [key: string]: number };
  tasksByDay: { [key: string]: number };
  topPerformers: Array<{
    userId: number;
    email: string;
    completedCount: number;
  }>;
}

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [
    CommonModule,
    NgbDropdownModule,
    NgbProgressbarModule,
    FeatherIconDirective
  ],
  templateUrl: './tasks.component.html',
  styleUrls: ['./tasks.component.scss']
})
export class TasksComponent implements OnInit {

  data: TaskPerformanceData | null = null;
  loading = true;
  error: string | null = null;

  constructor(private authService: AuthService) {}

  // Chart data
  departmentChartData: Array<{ label: string; value: number; color: string }> = [];
  priorityChartData: Array<{ label: string; value: number; color: string }> = [];

  private colors = [
    '#25D466', // Primary green
    '#17a2b8', // Info blue
    '#ffc107', // Warning yellow
    '#dc3545', // Danger red
    '#6f42c1', // Purple
    '#fd7e14', // Orange
    '#20c997', // Teal
  ];

  ngOnInit() {
    this.loadTaskPerformanceData();
  }

  private async loadTaskPerformanceData() {
    try {
      this.loading = true;
      this.error = null;

      const token = this.authService.getToken();
      if (!token) {
        throw new Error('No authentication token available. Please log in again.');
      }

      const response = await fetch(`${environment.apiUrl}/reports/tasks`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Authentication required. Please log in again.');
        } else if (response.status === 403) {
          throw new Error('Access forbidden. You do not have permission to view this report.');
        } else if (response.status === 404) {
          throw new Error('Report endpoint not found.');
        } else {
          const errorData = await response.json().catch(() => null);
          throw new Error(errorData?.message || `Failed to load task performance data (${response.status})`);
        }
      }

      this.data = await response.json();
      this.prepareChartData();
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'An error occurred';
      console.error('Error loading task performance data:', error);
    } finally {
      this.loading = false;
    }
  }

  private prepareChartData() {
    if (!this.data) return;

    // Prepare department chart data
    this.departmentChartData = Object.entries(this.data.tasksByDepartment).map(([dept, count], index) => ({
      label: dept,
      value: count,
      color: this.colors[index % this.colors.length]
    }));

    // Prepare priority chart data
    this.priorityChartData = Object.entries(this.data.tasksByPriority).map(([priority, count], index) => ({
      label: priority,
      value: count,
      color: this.getPriorityColor(priority)
    }));
  }

  private getPriorityColor(priority: string): string {
    switch (priority.toLowerCase()) {
      case 'low': return '#28a745';
      case 'normal': return '#17a2b8';
      case 'high': return '#ffc107';
      case 'urgent': return '#dc3545';
      default: return '#6c757d';
    }
  }

  getCompletionRateColor(): string {
    if (!this.data) return '#6c757d';
    if (this.data.completionRate >= 80) return '#25D466';
    if (this.data.completionRate >= 60) return '#ffc107';
    return '#dc3545';
  }

  formatCompletionTime(hours: number): string {
    if (hours < 1) {
      return `${Math.round(hours * 60)}m`;
    } else if (hours < 24) {
      return `${hours.toFixed(1)}h`;
    } else {
      const days = Math.floor(hours / 24);
      const remainingHours = Math.round(hours % 24);
      return `${days}d ${remainingHours}h`;
    }
  }

  getDaysFromTaskData(): Array<{ label: string; value: number }> {
    if (!this.data) return [];

    return Object.entries(this.data.tasksByDay)
      .map(([date, count]) => ({
        label: new Date(date).toLocaleDateString('en-ZA', { month: 'short', day: 'numeric', timeZone: 'Africa/Johannesburg' }),
        value: count
      }))
      .sort((a, b) => new Date(a.label).getTime() - new Date(b.label).getTime())
      .slice(-7); // Last 7 days
  }

  refresh() {
    this.loadTaskPerformanceData();
  }

  exportData() {
    if (!this.data) return;

    const dataStr = JSON.stringify(this.data, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `task-performance-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  trackByLabel(index: number, item: { label: string }): string {
    return item.label;
  }

  getMaxValue(data: Array<{ value: number }>): number {
    return Math.max(...data.map(item => item.value));
  }
}
