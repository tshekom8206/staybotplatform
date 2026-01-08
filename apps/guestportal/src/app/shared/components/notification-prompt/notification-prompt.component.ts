import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { PushNotificationService } from '../../../core/services/push-notification.service';
import { TenantService, TenantInfo } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-notification-prompt',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (showPrompt) {
      <div class="notification-prompt">
        <div class="prompt-content">
          <div class="prompt-icon">
            <i class="bi bi-bell"></i>
          </div>
          <div class="prompt-text">
            <strong>Stay Updated</strong>
            <span>Get notified when your requests are ready</span>
          </div>
          <div class="prompt-actions">
            <button class="btn-enable" (click)="enableNotifications()">
              <i class="bi bi-bell-fill me-1"></i>
              Enable
            </button>
            <button class="btn-dismiss" (click)="dismiss()">
              Not now
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .notification-prompt {
      position: fixed;
      bottom: 70px;
      left: 0;
      right: 0;
      background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
      color: white;
      padding: 1rem;
      z-index: 9998;
      box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.2);
      animation: slideUp 0.3s ease-out;
    }

    @keyframes slideUp {
      from {
        transform: translateY(100%);
        opacity: 0;
      }
      to {
        transform: translateY(0);
        opacity: 1;
      }
    }

    .prompt-content {
      display: flex;
      align-items: center;
      gap: 1rem;
      max-width: 600px;
      margin: 0 auto;
    }

    .prompt-icon {
      flex-shrink: 0;
      width: 48px;
      height: 48px;
      background: rgba(255, 255, 255, 0.2);
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .prompt-icon i {
      font-size: 1.5rem;
    }

    .prompt-text {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .prompt-text strong {
      font-size: 1rem;
    }

    .prompt-text span {
      font-size: 0.85rem;
      opacity: 0.9;
    }

    .prompt-actions {
      display: flex;
      gap: 0.5rem;
      flex-shrink: 0;
    }

    .btn-enable {
      background: white;
      color: #6366f1;
      border: none;
      padding: 0.5rem 1rem;
      border-radius: 20px;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
      display: flex;
      align-items: center;
    }

    .btn-enable:hover {
      transform: scale(1.05);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
    }

    .btn-dismiss {
      background: transparent;
      color: white;
      border: 1px solid rgba(255, 255, 255, 0.4);
      padding: 0.5rem 1rem;
      border-radius: 20px;
      font-size: 0.85rem;
      cursor: pointer;
      transition: background 0.2s;
    }

    .btn-dismiss:hover {
      background: rgba(255, 255, 255, 0.1);
    }

    /* Mobile adjustments */
    @media (max-width: 480px) {
      .notification-prompt {
        padding: 0.75rem;
      }

      .prompt-content {
        flex-wrap: wrap;
      }

      .prompt-icon {
        width: 40px;
        height: 40px;
      }

      .prompt-icon i {
        font-size: 1.25rem;
      }

      .prompt-text {
        flex: 1 1 calc(100% - 56px);
      }

      .prompt-text strong {
        font-size: 0.95rem;
      }

      .prompt-text span {
        font-size: 0.8rem;
      }

      .prompt-actions {
        flex: 1 1 100%;
        justify-content: center;
        margin-top: 0.5rem;
      }

      .btn-enable, .btn-dismiss {
        flex: 1;
        justify-content: center;
        max-width: 150px;
      }
    }
  `]
})
export class NotificationPromptComponent implements OnInit, OnDestroy {
  private pushService = inject(PushNotificationService);
  private tenantService = inject(TenantService);
  private destroy$ = new Subject<void>();

  showPrompt = false;
  tenantName = 'Guest Portal';

  ngOnInit(): void {
    // Delay showing the prompt to not overwhelm the user
    setTimeout(() => {
      this.checkIfShouldShow();
    }, 3000);

    // Get tenant name for personalized prompt
    this.tenantService.tenant$
      .pipe(takeUntil(this.destroy$))
      .subscribe((tenant: TenantInfo | null) => {
        if (tenant) {
          this.tenantName = tenant.name;
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private checkIfShouldShow(): void {
    this.showPrompt = this.pushService.shouldShowPrompt();
  }

  async enableNotifications(): Promise<void> {
    this.pushService.markPromptSeen();
    const success = await this.pushService.subscribe();

    if (success) {
      this.showPrompt = false;
    } else {
      // If failed (user denied), still hide the prompt
      this.showPrompt = false;
    }
  }

  dismiss(): void {
    this.pushService.markPromptSeen();
    this.showPrompt = false;
  }
}
