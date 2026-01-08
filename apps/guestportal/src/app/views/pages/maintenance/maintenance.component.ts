import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { RoomContextService } from '../../../core/services/room-context.service';
import { GuestApiService } from '../../../core/services/guest-api.service';
import { AnalyticsService } from '../../../core/services/analytics.service';

interface MaintenanceIssue {
  id: string;
  labelKey: string;
  icon: string;
  selected: boolean;
}

@Component({
  selector: 'app-maintenance',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header with Glassmorphism -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'maintenance.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'maintenance.subtitle' | translate }}</p>
        </div>

        @if (!submitted()) {
          <form (ngSubmit)="submitRequest()">
            <!-- Room Number -->
            <div class="mb-4">
              <label class="form-label">{{ 'common.roomNumber' | translate }}</label>
              <input type="text"
                     class="form-control form-control-lg"
                     [(ngModel)]="roomNumber"
                     name="roomNumber"
                     [placeholder]="'common.enterRoomNumber' | translate"
                     required>
            </div>

            <!-- Issue Selection -->
            <div class="mb-4">
              <label class="form-label">{{ 'maintenance.selectIssues' | translate }}</label>
              <div class="issues-grid">
                @for (issue of issues; track issue.id) {
                  <button type="button"
                          class="issue-card"
                          [class.selected]="issue.selected"
                          (click)="toggleIssue(issue)">
                    <i class="bi" [class]="'bi-' + issue.icon"></i>
                    <span>{{ issue.labelKey | translate }}</span>
                  </button>
                }
              </div>
            </div>

            <!-- Additional Details -->
            <div class="mb-4">
              <label class="form-label">{{ 'maintenance.otherDetails' | translate }}</label>
              <textarea class="form-control"
                        [(ngModel)]="additionalDetails"
                        name="details"
                        rows="3"
                        [placeholder]="'maintenance.otherPlaceholder' | translate"></textarea>
            </div>

            <!-- Submit Button -->
            <button type="submit"
                    class="btn btn-primary btn-lg w-100"
                    [disabled]="!canSubmit || submitting()">
              @if (submitting()) {
                <span class="spinner-border spinner-border-sm me-2"></span>
              }
              {{ 'maintenance.submitRequest' | translate }}
            </button>
          </form>
        } @else {
          <!-- Success Message -->
          <div class="success-card text-center">
            <div class="success-icon">
              <i class="bi bi-check-circle-fill"></i>
            </div>
            <h2>{{ 'maintenance.success' | translate }}</h2>
            <p class="text-muted">{{ 'maintenance.successMessage' | translate }}</p>
            @if (estimatedResponse()) {
              <p class="estimated-time">
                <i class="bi bi-clock"></i> Estimated response: {{ estimatedResponse() }}
              </p>
            }
            <button class="btn btn-outline-primary" (click)="reset()">
              {{ 'maintenance.reportAnother' | translate }}
            </button>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1rem 0; }

    /* Page Header - Clean, floating on background */
    .page-header {
      padding: 1.5rem 0 1.25rem;
      margin-bottom: 1rem;
    }
    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-decoration: none;
      font-size: 0.9rem;
      font-weight: 500;
      padding: 0.4rem 0.75rem;
      margin: -0.4rem -0.75rem 0.75rem;
      border-radius: 50px;
      transition: all 0.2s ease;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }
    .back-link:hover { background: rgba(255, 255, 255, 0.15); color: white; }
    .back-link i { font-size: 1rem; }
    .page-title {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }
    .page-subtitle {
      font-size: 0.95rem;
      color: rgba(255, 255, 255, 0.9);
      margin: 0.25rem 0 0;
      text-shadow: 0 1px 6px rgba(0, 0, 0, 0.3);
    }

    /* Form Labels - Visible on blurred background */
    .form-label {
      display: block;
      color: white;
      font-weight: 600;
      font-size: 0.95rem;
      margin-bottom: 0.5rem;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.5);
    }

    .issues-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 0.75rem;
    }

    .issue-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 1rem;
      border: 2px solid #e9ecef;
      border-radius: 12px;
      background: white;
      cursor: pointer;
      transition: all 0.2s;
      position: relative;
    }
    .issue-card:hover {
      border-color: #888;
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }
    .issue-card.selected {
      border-color: #27ae60;
      border-width: 3px;
      background: linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%);
      transform: scale(1.02);
      box-shadow: 0 4px 16px rgba(39, 174, 96, 0.3);
    }
    .issue-card.selected::after {
      content: '\\F272';
      font-family: 'bootstrap-icons';
      position: absolute;
      top: -8px;
      right: -8px;
      width: 24px;
      height: 24px;
      background: #27ae60;
      color: white;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.75rem;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
    }
    .issue-card i {
      width: 44px;
      height: 44px;
      background: #1a1a1a;
      color: white;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.2rem;
      margin-bottom: 0.5rem;
      transition: all 0.2s;
    }
    .issue-card.selected i {
      background: #27ae60;
    }
    .issue-card span {
      font-size: 0.875rem;
      color: #333;
      font-weight: 500;
      transition: all 0.2s;
    }
    .issue-card.selected span {
      color: #1b5e20;
      font-weight: 600;
    }

    /* Form Controls */
    .form-control {
      background: rgba(255, 255, 255, 0.95);
      border: 2px solid transparent;
      border-radius: 12px;
    }
    .form-control:focus {
      border-color: #27ae60;
      box-shadow: 0 0 0 3px rgba(39, 174, 96, 0.2);
    }

    /* Submit Button */
    .btn-primary {
      background: linear-gradient(135deg, #27ae60 0%, #1e8449 100%);
      border: none;
      border-radius: 12px;
      padding: 0.875rem;
      font-weight: 600;
      box-shadow: 0 4px 12px rgba(39, 174, 96, 0.3);
      transition: all 0.2s;
    }
    .btn-primary:hover {
      transform: translateY(-2px);
      box-shadow: 0 6px 16px rgba(39, 174, 96, 0.4);
    }
    .btn-primary:disabled {
      background: #ccc;
      transform: none;
      box-shadow: none;
    }

    .success-card {
      padding: 3rem 1rem;
      background: linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%);
      border-radius: 16px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
    }
    .success-icon {
      font-size: 4rem;
      color: #27ae60;
      margin-bottom: 1rem;
    }
    .success-card h2 {
      color: #27ae60;
      margin-bottom: 0.5rem;
    }
    .estimated-time {
      margin-top: 1rem;
      color: var(--theme-primary, #1976d2);
      font-weight: 500;
    }
    .estimated-time i {
      margin-right: 0.25rem;
    }
  `]
})
export class MaintenanceComponent {
  private roomContextService = inject(RoomContextService);
  private apiService = inject(GuestApiService);
  private analyticsService = inject(AnalyticsService);

  roomNumber = this.roomContextService.getRoomNumber() || '';
  additionalDetails = '';
  submitted = signal(false);
  submitting = signal(false);
  estimatedResponse = signal('');
  private formStartTracked = false;

  issues: MaintenanceIssue[] = [
    { id: 'plumbing', labelKey: 'maintenance.categories.plumbing', icon: 'droplet', selected: false },
    { id: 'electrical', labelKey: 'maintenance.categories.electrical', icon: 'lightning', selected: false },
    { id: 'aircon', labelKey: 'maintenance.categories.aircon', icon: 'snow', selected: false },
    { id: 'tv', labelKey: 'maintenance.categories.tv', icon: 'tv', selected: false },
    { id: 'wifi', labelKey: 'maintenance.categories.wifi', icon: 'wifi', selected: false },
    { id: 'cleaning', labelKey: 'maintenance.categories.cleaning', icon: 'brush', selected: false },
    { id: 'other', labelKey: 'maintenance.categories.other', icon: 'three-dots', selected: false }
  ];

  get canSubmit(): boolean {
    return !!this.roomNumber && this.issues.some(i => i.selected);
  }

  toggleIssue(issue: MaintenanceIssue): void {
    this.trackFormStart();
    // Deselect all other issues first (single selection only)
    this.issues.forEach(i => i.selected = false);
    // Select the clicked issue
    issue.selected = true;
  }

  private trackFormStart(): void {
    if (!this.formStartTracked) {
      this.formStartTracked = true;
      this.analyticsService.trackEvent('form_start', {
        form_name: 'maintenance_request',
        page_location: '/maintenance'
      });
    }
  }

  submitRequest(): void {
    if (!this.canSubmit) return;

    // Save room number for future use
    if (this.roomNumber) {
      this.roomContextService.setRoomNumber(this.roomNumber);
    }

    this.submitting.set(true);

    const selectedIssues = this.issues.filter(i => i.selected).map(i => i.id);

    // Track maintenance request in GA4
    this.analyticsService.trackMaintenanceRequest(selectedIssues, this.roomNumber);

    this.apiService.submitMaintenanceRequest({
      roomNumber: this.roomNumber,
      issues: selectedIssues,
      description: this.additionalDetails || undefined
    }).subscribe({
      next: (response) => {
        this.submitting.set(false);
        if (response.success) {
          this.submitted.set(true);
          this.estimatedResponse.set(response.estimatedResponse);
        }
      },
      error: (error) => {
        console.error('Failed to submit maintenance request:', error);
        this.submitting.set(false);
        // Still show success for now (API might be offline)
        this.submitted.set(true);
      }
    });
  }

  reset(): void {
    this.submitted.set(false);
    this.estimatedResponse.set('');
    this.issues.forEach(i => i.selected = false);
    this.additionalDetails = '';
  }
}
