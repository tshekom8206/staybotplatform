import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgbDropdownModule, NgbProgressbarModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../../core/services/auth.service';

interface SatisfactionData {
  totalRatings: number;
  averageRating: number;
  responseRate: number;
  ratingsDistribution: { [key: string]: number };
  ratingsBySource: Array<{
    source: string;
    count: number;
    avgScore: number;
  }>;
  ratingsByDay: { [key: string]: { count: number; avgScore: number } };
  satisfactionByBookingSource: Array<{
    source: string;
    count: number;
    avgScore: number;
  }>;
  recentFeedback: Array<{
    score: number;
    comment: string;
    source: string;
    guestPhone: string | null;
    guestName: string | null;
    roomNumber: string | null;
    receivedAt: string;
  }>;
}

@Component({
  selector: 'app-satisfaction',
  standalone: true,
  imports: [
    CommonModule,
    NgbDropdownModule,
    NgbProgressbarModule,
    FeatherIconDirective
  ],
  templateUrl: './satisfaction.component.html',
  styleUrls: ['./satisfaction.component.scss']
})
export class SatisfactionComponent implements OnInit {

  data: SatisfactionData | null = null;
  loading = true;
  error: string | null = null;

  constructor(private authService: AuthService) {}

  // Chart data
  ratingsChartData: Array<{ label: string; value: number; color: string }> = [];
  sourceChartData: Array<{ label: string; value: number; color: string }> = [];

  private colors = [
    '#25D466', // Primary green
    '#17a2b8', // Info blue
    '#ffc107', // Warning yellow
    '#dc3545', // Danger red
    '#6f42c1', // Purple
    '#fd7e14', // Orange
    '#20c997', // Teal
  ];

  private ratingColors = [
    '#dc3545', // 1 star - red
    '#fd7e14', // 2 stars - orange
    '#ffc107', // 3 stars - yellow
    '#17a2b8', // 4 stars - blue
    '#25D466', // 5 stars - green
  ];

  ngOnInit() {
    this.loadSatisfactionData();
  }

  private async loadSatisfactionData() {
    try {
      this.loading = true;
      this.error = null;

      const token = this.authService.getToken();
      if (!token) {
        throw new Error('No authentication token available. Please log in again.');
      }

      const response = await fetch(`${environment.apiUrl}/reports/satisfaction`, {
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
          throw new Error(errorData?.message || `Failed to load satisfaction data (${response.status})`);
        }
      }

      this.data = await response.json();
      this.prepareChartData();
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'An error occurred';
      console.error('Error loading satisfaction data:', error);
    } finally {
      this.loading = false;
    }
  }

  private prepareChartData() {
    if (!this.data) return;

    // Prepare ratings distribution chart data
    this.ratingsChartData = Object.entries(this.data.ratingsDistribution)
      .sort(([a], [b]) => parseInt(a) - parseInt(b))
      .map(([rating, count], index) => ({
        label: `${rating} Star${parseInt(rating) !== 1 ? 's' : ''}`,
        value: count,
        color: this.ratingColors[parseInt(rating) - 1] || this.colors[index % this.colors.length]
      }));

    // Prepare source chart data
    this.sourceChartData = this.data.ratingsBySource.map((source, index) => ({
      label: this.formatSourceLabel(source.source),
      value: source.count,
      color: this.colors[index % this.colors.length]
    }));
  }

  formatSourceLabel(source: string): string {
    switch (source.toLowerCase()) {
      case 'checkout': return 'Check-out';
      case 'manual': return 'Manual Entry';
      case 'whatsapp': return 'WhatsApp';
      default: return source.charAt(0).toUpperCase() + source.slice(1);
    }
  }

  getAverageRatingColor(): string {
    if (!this.data) return '#6c757d';
    if (this.data.averageRating >= 4.5) return '#25D466';
    if (this.data.averageRating >= 4.0) return '#17a2b8';
    if (this.data.averageRating >= 3.5) return '#ffc107';
    if (this.data.averageRating >= 3.0) return '#fd7e14';
    return '#dc3545';
  }

  getResponseRateColor(): string {
    if (!this.data) return '#6c757d';
    if (this.data.responseRate >= 80) return '#25D466';
    if (this.data.responseRate >= 60) return '#ffc107';
    return '#dc3545';
  }

  getStarArray(rating: number): number[] {
    return Array(5).fill(0).map((_, i) => i + 1);
  }

  isStarFilled(starNumber: number, rating: number): boolean {
    return starNumber <= Math.floor(rating);
  }

  isStarHalf(starNumber: number, rating: number): boolean {
    return starNumber === Math.floor(rating) + 1 && rating % 1 >= 0.5;
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-ZA', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Africa/Johannesburg'
    });
  }

  getTrendsFromData(): Array<{ label: string; value: number }> {
    if (!this.data) return [];

    return Object.entries(this.data.ratingsByDay)
      .map(([date, data]) => ({
        label: new Date(date).toLocaleDateString('en-ZA', { month: 'short', day: 'numeric', timeZone: 'Africa/Johannesburg' }),
        value: data.avgScore
      }))
      .sort((a, b) => new Date(a.label).getTime() - new Date(b.label).getTime())
      .slice(-7); // Last 7 days
  }

  getDepartmentRatings(): Array<{ label: string; value: number; color: string }> {
    if (!this.data) return [];

    return this.data.satisfactionByBookingSource.map((source, index) => ({
      label: source.source,
      value: source.avgScore,
      color: this.colors[index % this.colors.length]
    }));
  }

  refresh() {
    this.loadSatisfactionData();
  }

  exportData() {
    if (!this.data) return;

    const dataStr = JSON.stringify(this.data, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `satisfaction-report-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  trackByLabel(index: number, item: { label: string }): string {
    return item.label;
  }

  trackByFeedback(index: number, item: {
    score: number;
    comment: string;
    source: string;
    guestPhone: string | null;
    guestName: string | null;
    roomNumber: string | null;
    receivedAt: string
  }): string {
    return (item.guestPhone || 'unknown') + item.receivedAt;
  }

  getMaxValue(data: Array<{ value: number }>): number {
    return Math.max(...data.map(item => item.value));
  }

  getMaxTrendValue(): number {
    const trends = this.getTrendsFromData();
    return Math.max(...trends.map(item => item.value), 5); // Max 5 for ratings
  }

  getValidFeedback(): Array<{
    score: number;
    comment: string;
    source: string;
    guestPhone: string | null;
    guestName: string | null;
    roomNumber: string | null;
    receivedAt: string;
  }> {
    if (!this.data?.recentFeedback) return [];

    return this.data.recentFeedback.filter(feedback =>
      feedback.comment &&
      feedback.comment.trim() !== '' &&
      (feedback.guestName || feedback.guestPhone || feedback.roomNumber)
    );
  }

  getGuestInitial(feedback: any): string {
    if (feedback.guestName) {
      return feedback.guestName.charAt(0).toUpperCase();
    }
    if (feedback.guestPhone) {
      return feedback.guestPhone.charAt(feedback.guestPhone.length - 1).toUpperCase();
    }
    return 'G';
  }

  getGuestDisplayName(feedback: any): string {
    if (feedback.guestName) {
      return feedback.guestName;
    }
    if (feedback.guestPhone) {
      return feedback.guestPhone;
    }
    return 'Guest';
  }
}
