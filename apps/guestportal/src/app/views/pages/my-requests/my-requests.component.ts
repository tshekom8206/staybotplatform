import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService } from '../../../core/services/guest-api.service';
import { RoomContextService } from '../../../core/services/room-context.service';

interface StaffTask {
  id: number;
  title: string;
  description: string;
  status: string; // "Open", "In Progress", "Completed"
  department: string;
  taskType: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
}

@Component({
  selector: 'app-my-requests',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'myRequests.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'myRequests.subtitle' | translate }}</p>
        </div>

        @if (loading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (error()) {
          <div class="error-container">
            <div class="error-card">
              <i class="bi bi-exclamation-triangle"></i>
              <h3>{{ 'common.error' | translate }}</h3>
              <p>{{ error() }}</p>
              <a routerLink="/" class="btn btn-dark">
                {{ 'common.backToHome' | translate }}
              </a>
            </div>
          </div>
        } @else if (tasks().length === 0) {
          <!-- Empty State -->
          <div class="empty-container">
            <div class="empty-card">
              <i class="bi bi-inbox"></i>
              <h3>{{ 'myRequests.empty' | translate }}</h3>
              <p>{{ 'myRequests.emptyDescription' | translate }}</p>
              <a routerLink="/" class="btn btn-primary">
                {{ 'common.backToHome' | translate }}
              </a>
            </div>
          </div>
        } @else {
          <!-- Requests List -->
          <div class="requests-section">
            @for (task of tasks(); track task.id) {
              <div class="request-card">
                <div class="request-header">
                  <div class="request-info">
                    <h3 class="request-title">{{ task.title }}</h3>
                    <span class="request-time">
                      <i class="bi bi-clock"></i>
                      {{ 'myRequests.requestedAt' | translate }}: {{ task.createdAt | date:'short' }}
                    </span>
                  </div>
                  <span class="status-badge" [class]="'status-' + getStatusClass(task.status)">
                    {{ getStatusLabel(task.status) | translate }}
                  </span>
                </div>

                @if (task.description) {
                  <p class="request-description">{{ task.description }}</p>
                }

                <div class="request-meta">
                  <span class="meta-item">
                    <i class="bi bi-building"></i>
                    {{ task.department }}
                  </span>
                  @if (task.completedAt) {
                    <span class="meta-item">
                      <i class="bi bi-check-circle"></i>
                      {{ 'myRequests.completedAt' | translate }}: {{ task.completedAt | date:'short' }}
                    </span>
                  }
                </div>
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 1rem 0;
    }

    /* Page Header - White text on gradient */
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

    .back-link:hover {
      background: rgba(255, 255, 255, 0.15);
      color: white;
    }

    .back-link i {
      font-size: 1rem;
    }

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

    /* Requests Section */
    .requests-section {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .request-card {
      background: white;
      border-radius: 12px;
      padding: 1.25rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      transition: all 0.2s ease;
    }

    .request-card:hover {
      box-shadow: 0 4px 12px rgba(0,0,0,0.12);
    }

    .request-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 0.75rem;
    }

    .request-info {
      flex: 1;
    }

    .request-title {
      font-size: 1.1rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 0.5rem;
    }

    .request-time {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
      color: #666;
    }

    .request-time i {
      font-size: 0.9rem;
    }

    .status-badge {
      padding: 0.4rem 0.875rem;
      border-radius: 50px;
      font-size: 0.85rem;
      font-weight: 500;
      white-space: nowrap;
    }

    .status-badge.status-open {
      background: #f8f9fa;
      color: #666;
    }

    .status-badge.status-in-progress {
      background: #e3f2fd;
      color: #1976d2;
    }

    .status-badge.status-completed {
      background: #f0fdf4;
      color: #16a34a;
    }

    .request-description {
      font-size: 0.95rem;
      color: #666;
      margin: 0 0 1rem;
      line-height: 1.5;
    }

    .request-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      padding-top: 0.75rem;
      border-top: 1px solid #e9ecef;
    }

    .meta-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
      color: #666;
    }

    .meta-item i {
      font-size: 0.9rem;
      color: #888;
    }

    /* Empty State */
    .empty-container {
      padding: 3rem 0;
    }

    .empty-card {
      background: white;
      border-radius: 12px;
      padding: 3rem 2rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      text-align: center;
    }

    .empty-card i {
      font-size: 4rem;
      color: #adb5bd;
      margin-bottom: 1rem;
    }

    .empty-card h3 {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 0.5rem;
    }

    .empty-card p {
      color: #666;
      margin: 0 0 1.5rem;
    }

    .btn-primary {
      background: #333;
      border-color: #333;
      color: white;
      padding: 0.75rem 1.5rem;
      border-radius: 50px;
      font-weight: 500;
      text-decoration: none;
      display: inline-block;
      transition: all 0.2s ease;
    }

    .btn-primary:hover {
      background: #1a1a1a;
      color: white;
      transform: translateY(-1px);
      box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    }

    /* Error State */
    .error-container {
      padding: 2rem 0;
    }

    .error-card {
      background: white;
      border-radius: 12px;
      padding: 2rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      text-align: center;
    }

    .error-card i {
      font-size: 3rem;
      color: #e74c3c;
      margin-bottom: 1rem;
    }

    .error-card h3 {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 0.5rem;
    }

    .error-card p {
      color: #666;
      margin: 0 0 1.5rem;
    }

    .btn-dark {
      background: #333;
      border-color: #333;
      color: white;
      padding: 0.75rem 1.5rem;
      border-radius: 50px;
      font-weight: 500;
      text-decoration: none;
      display: inline-block;
      transition: all 0.2s ease;
    }

    .btn-dark:hover {
      background: #1a1a1a;
      color: white;
      transform: translateY(-1px);
      box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    }

    /* Mobile Adjustments */
    @media (max-width: 768px) {
      .page-title {
        font-size: 1.5rem;
      }

      .request-header {
        flex-direction: column;
        align-items: flex-start;
      }

      .status-badge {
        align-self: flex-start;
      }

      .request-meta {
        flex-direction: column;
        gap: 0.5rem;
      }
    }
  `]
})
export class MyRequestsComponent implements OnInit, OnDestroy {
  tasks = signal<StaffTask[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  private pollingInterval?: number;

  private apiService = inject(GuestApiService);
  private roomContext = inject(RoomContextService);

  ngOnInit(): void {
    const roomNumber = this.roomContext.getRoomNumber();
    if (!roomNumber) {
      this.error.set('Room number is required. Please return to home and enter your room number.');
      this.loading.set(false);
      return;
    }

    this.loadTasks(roomNumber);

    // Poll every 30 seconds for updates
    this.pollingInterval = window.setInterval(() => {
      this.loadTasks(roomNumber, true);
    }, 30000);
  }

  ngOnDestroy(): void {
    if (this.pollingInterval) {
      clearInterval(this.pollingInterval);
    }
  }

  loadTasks(roomNumber: string, silent: boolean = false): void {
    if (!silent) {
      this.loading.set(true);
    }

    this.apiService.getTasksByRoomNumber(roomNumber).subscribe({
      next: (tasks) => {
        // Sort by createdAt (newest first)
        const sortedTasks = tasks.sort((a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        this.tasks.set(sortedTasks);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading tasks:', err);
        this.error.set('Unable to load your requests. Please try again.');
        this.loading.set(false);
      }
    });
  }

  getStatusClass(status: string): string {
    const statusLower = status.toLowerCase().replace(/\s+/g, '-');
    if (statusLower === 'in-progress' || statusLower === 'inprogress') {
      return 'in-progress';
    }
    return statusLower;
  }

  getStatusLabel(status: string): string {
    const statusLower = status.toLowerCase();
    if (statusLower === 'open') {
      return 'myRequests.status.open';
    } else if (statusLower === 'in progress' || statusLower === 'inprogress') {
      return 'myRequests.status.inProgress';
    } else if (statusLower === 'completed') {
      return 'myRequests.status.completed';
    }
    return status;
  }
}
