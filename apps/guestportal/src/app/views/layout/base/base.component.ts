import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from '../header/header.component';
import { FooterComponent } from '../footer/footer.component';
import { RoomPromptBannerComponent } from '../../../shared/components/room-prompt-banner/room-prompt-banner.component';
import { PwaInstallPromptComponent } from '../../../shared/components/pwa-install-prompt/pwa-install-prompt.component';
import { PwaUpdateNotificationComponent } from '../../../shared/components/pwa-update-notification/pwa-update-notification.component';
import { NotificationPromptComponent } from '../../../shared/components/notification-prompt/notification-prompt.component';
import { TenantService, TenantInfo } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-base',
  standalone: true,
  imports: [CommonModule, RouterOutlet, HeaderComponent, FooterComponent, RoomPromptBannerComponent, PwaInstallPromptComponent, PwaUpdateNotificationComponent, NotificationPromptComponent],
  template: `
    <div class="app-wrapper" [class.has-background]="tenant?.backgroundImageUrl">
      <!-- Blurry Background Image -->
      @if (tenant?.backgroundImageUrl) {
        <div class="background-layer">
          <div class="background-image" [style.background-image]="'url(' + tenant?.backgroundImageUrl + ')'"></div>
          <div class="background-overlay"></div>
        </div>
      }

      @if (loading) {
        <div class="loading-screen">
          <div class="loading-content">
            <div class="spinner-border text-white" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
            <p class="mt-3 text-white">Loading your experience...</p>
          </div>
        </div>
      } @else if (error) {
        <div class="error-screen">
          <div class="error-content text-center">
            <div class="error-icon">
              <i class="bi bi-exclamation-circle"></i>
            </div>
            <h2>Oops!</h2>
            <p class="text-muted">{{ error }}</p>
            <button class="btn btn-primary btn-lg" (click)="retry()">Try Again</button>
          </div>
        </div>
      } @else {
        <app-header />
        <app-room-prompt-banner />
        <app-pwa-update-notification />
        <main class="main-content">
          <router-outlet />
        </main>
        <app-footer />
        <app-notification-prompt />
        <app-pwa-install-prompt />
      }
    </div>
  `,
  styles: [`
    .app-wrapper {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      position: relative;
      background: #f8f9fa;
    }

    .app-wrapper.has-background {
      background: transparent;
    }

    /* Blurry Background Layer */
    .background-layer {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      z-index: -1;
      overflow: hidden;
    }

    .background-image {
      position: absolute;
      top: -5px;
      left: -5px;
      right: -5px;
      bottom: -5px;
      background-size: cover;
      background-position: center;
      filter: blur(1.5px);
    }

    .background-overlay {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.1);
    }

    .main-content {
      flex: 1;
      padding: 1rem 0;
      position: relative;
      z-index: 1;
    }

    /* Loading Screen */
    .loading-screen {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    }

    .loading-content {
      text-align: center;
    }

    /* Error Screen */
    .error-screen {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #f8f9fa;
    }

    .error-content {
      padding: 2rem;
    }

    .error-icon {
      font-size: 4rem;
      color: #e74c3c;
      margin-bottom: 1rem;
    }

    .error-content h2 {
      font-weight: 700;
      margin-bottom: 0.5rem;
    }
  `]
})
export class BaseComponent implements OnInit {
  private tenantService = inject(TenantService);

  loading = true;
  error: string | null = null;
  tenant: TenantInfo | null = null;

  ngOnInit(): void {
    this.loadTenant();
  }

  loadTenant(): void {
    this.loading = true;
    this.error = null;

    this.tenantService.loadTenantInfo().subscribe({
      next: (tenant) => {
        if (!tenant) {
          this.error = 'Hotel not found';
        } else {
          this.tenant = tenant;
        }
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load hotel information';
        this.loading = false;
      }
    });
  }

  retry(): void {
    this.loadTenant();
  }
}
