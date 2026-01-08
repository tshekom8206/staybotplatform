import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { PwaService } from '../../../core/services/pwa.service';

@Component({
  selector: 'app-pwa-update-notification',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    @if (showNotification) {
      <div class="pwa-update-notification">
        <div class="notification-content">
          <div class="notification-icon">
            <i class="bi bi-arrow-clockwise"></i>
          </div>
          <div class="notification-text">
            <strong>{{ 'pwa.update.title' | translate }}</strong>
            <span>{{ 'pwa.update.description' | translate }}</span>
          </div>
          <div class="notification-actions">
            <button class="btn-update" (click)="updateApp()">
              <i class="bi bi-arrow-repeat me-1"></i>
              {{ 'pwa.update.updateButton' | translate }}
            </button>
            <button class="btn-later" (click)="dismissUpdate()">
              {{ 'pwa.update.later' | translate }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .pwa-update-notification {
      position: fixed;
      top: 70px;
      left: 50%;
      transform: translateX(-50%);
      background: linear-gradient(135deg, #2e7d32 0%, #1b5e20 100%);
      color: white;
      padding: 0.75rem 1rem;
      border-radius: 12px;
      z-index: 9999;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.25);
      animation: slideDown 0.3s ease-out;
      max-width: calc(100% - 2rem);
      width: 400px;
    }

    @keyframes slideDown {
      from {
        transform: translateX(-50%) translateY(-100%);
        opacity: 0;
      }
      to {
        transform: translateX(-50%) translateY(0);
        opacity: 1;
      }
    }

    .notification-content {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .notification-icon {
      flex-shrink: 0;
      width: 40px;
      height: 40px;
      background: rgba(255, 255, 255, 0.2);
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .notification-icon i {
      font-size: 1.25rem;
      animation: spin 2s linear infinite;
    }

    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }

    .notification-text {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
    }

    .notification-text strong {
      font-size: 0.95rem;
    }

    .notification-text span {
      font-size: 0.8rem;
      opacity: 0.9;
    }

    .notification-actions {
      display: flex;
      gap: 0.5rem;
      flex-shrink: 0;
    }

    .btn-update {
      background: white;
      color: #2e7d32;
      border: none;
      padding: 0.4rem 0.75rem;
      border-radius: 16px;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
      display: flex;
      align-items: center;
    }

    .btn-update:hover {
      transform: scale(1.05);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
    }

    .btn-later {
      background: transparent;
      color: white;
      border: none;
      padding: 0.4rem 0.5rem;
      font-size: 0.8rem;
      cursor: pointer;
      opacity: 0.8;
      transition: opacity 0.2s;
    }

    .btn-later:hover {
      opacity: 1;
    }

    /* Mobile adjustments */
    @media (max-width: 480px) {
      .pwa-update-notification {
        top: 65px;
        width: calc(100% - 1rem);
        left: 0.5rem;
        right: 0.5rem;
        transform: none;
        padding: 0.6rem 0.75rem;
      }

      @keyframes slideDown {
        from {
          transform: translateY(-100%);
          opacity: 0;
        }
        to {
          transform: translateY(0);
          opacity: 1;
        }
      }

      .notification-content {
        flex-wrap: wrap;
      }

      .notification-icon {
        width: 36px;
        height: 36px;
      }

      .notification-icon i {
        font-size: 1.1rem;
      }

      .notification-text {
        flex: 1 1 calc(100% - 52px);
      }

      .notification-text strong {
        font-size: 0.9rem;
      }

      .notification-text span {
        font-size: 0.75rem;
      }

      .notification-actions {
        flex: 1 1 100%;
        justify-content: flex-end;
        margin-top: 0.5rem;
      }
    }
  `]
})
export class PwaUpdateNotificationComponent implements OnInit, OnDestroy {
  private pwaService = inject(PwaService);
  private destroy$ = new Subject<void>();

  showNotification = false;

  ngOnInit(): void {
    // Listen for update availability
    this.pwaService.updateAvailable$
      .pipe(takeUntil(this.destroy$))
      .subscribe(available => {
        this.showNotification = available;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  async updateApp(): Promise<void> {
    this.showNotification = false;
    // Reload the page to get the latest version
    document.location.reload();
  }

  dismissUpdate(): void {
    this.pwaService.dismissUpdateNotification();
    this.showNotification = false;
  }
}
