import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { PwaService } from '../../../core/services/pwa.service';
import { TenantService, TenantInfo } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-pwa-install-prompt',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    @if (showPrompt && !isInstalled) {
      <div class="pwa-install-prompt" [@slideIn]>
        <div class="prompt-content">
          <div class="prompt-icon">
            <i class="bi bi-download"></i>
          </div>
          <div class="prompt-text">
            <strong>{{ 'pwa.install.title' | translate }}</strong>
            <span>{{ 'pwa.install.description' | translate: { hotelName: tenantName } }}</span>
          </div>
          <div class="prompt-actions">
            <button class="btn-install" (click)="installApp()">
              <i class="bi bi-plus-circle me-1"></i>
              {{ 'pwa.install.installButton' | translate }}
            </button>
            <button class="btn-dismiss" (click)="dismiss()">
              {{ 'pwa.install.notNow' | translate }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .pwa-install-prompt {
      position: fixed;
      bottom: 0;
      left: 0;
      right: 0;
      background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
      color: white;
      padding: 1rem;
      z-index: 9999;
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

    .btn-install {
      background: white;
      color: #1976d2;
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

    .btn-install:hover {
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
      .pwa-install-prompt {
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

      .btn-install, .btn-dismiss {
        flex: 1;
        justify-content: center;
        max-width: 150px;
      }
    }
  `]
})
export class PwaInstallPromptComponent implements OnInit, OnDestroy {
  private readonly DISMISS_KEY = 'pwa_install_dismissed';
  private pwaService = inject(PwaService);
  private tenantService = inject(TenantService);
  private destroy$ = new Subject<void>();

  showPrompt = false;
  isInstalled = false;
  tenantName = 'Guest Portal';

  ngOnInit(): void {
    // Check if already installed
    this.isInstalled = this.pwaService.isInstalled();

    // Check if dismissed this session
    const dismissed = sessionStorage.getItem(this.DISMISS_KEY) === 'true';
    if (dismissed || this.isInstalled) {
      return;
    }

    // Get tenant name for personalized prompt
    this.tenantService.tenant$
      .pipe(takeUntil(this.destroy$))
      .subscribe((tenant: TenantInfo | null) => {
        if (tenant) {
          this.tenantName = tenant.name;
        }
      });

    // Listen for install prompt availability
    this.pwaService.installPromptAvailable$
      .pipe(takeUntil(this.destroy$))
      .subscribe(available => {
        this.showPrompt = available && !this.isInstalled;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  async installApp(): Promise<void> {
    const installed = await this.pwaService.promptInstall();
    if (installed) {
      this.showPrompt = false;
      this.isInstalled = true;
    }
  }

  dismiss(): void {
    sessionStorage.setItem(this.DISMISS_KEY, 'true');
    this.showPrompt = false;
  }
}
