import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, WifiCredentials } from '../../../core/services/guest-api.service';

@Component({
  selector: 'app-wifi-card',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    @if (wifi() && wifi()!.network) {
      <div class="wifi-card">
        <div class="wifi-info">
          <div class="wifi-icon">
            <i class="bi bi-wifi"></i>
          </div>
          <div class="wifi-details">
            <span class="network-name">{{ wifi()!.network }}</span>
            <div class="password-row">
              <span class="password-value">
                {{ showPassword() ? wifi()!.password : '••••••••' }}
              </span>
              <button class="btn-icon" (click)="togglePassword()" [attr.aria-label]="showPassword() ? 'Hide' : 'Show'">
                <i class="bi" [class.bi-eye]="!showPassword()" [class.bi-eye-slash]="showPassword()"></i>
              </button>
            </div>
          </div>
        </div>
        <button class="btn-copy" (click)="copyPassword()" [class.copied]="copied()">
          <i class="bi" [class.bi-check2]="copied()" [class.bi-clipboard]="!copied()"></i>
          <span>{{ copied() ? ('wifi.copied' | translate) : ('wifi.copy' | translate) }}</span>
        </button>
      </div>
    }
  `,
  styles: [`
    :host {
      display: block;
      margin: 0 -1rem 1.25rem -1rem;
    }

    .wifi-card {
      background: rgba(255, 255, 255, 0.95);
      backdrop-filter: blur(20px);
      -webkit-backdrop-filter: blur(20px);
      border-radius: 0;
      padding: 1rem 1.25rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      box-shadow: 0 4px 24px rgba(0, 0, 0, 0.1);
    }

    .wifi-info {
      display: flex;
      align-items: center;
      gap: 0.875rem;
      flex: 1;
      min-width: 0;
    }

    .wifi-icon {
      width: 44px;
      height: 44px;
      background: #1a1a2e;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .wifi-icon i {
      font-size: 1.25rem;
      color: white;
    }

    .wifi-details {
      flex: 1;
      min-width: 0;
    }

    .network-name {
      display: block;
      font-size: 0.95rem;
      font-weight: 600;
      color: #1a1a2e;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      line-height: 1.3;
    }

    .password-row {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      margin-top: 0.125rem;
    }

    .password-value {
      font-size: 0.8rem;
      color: #666;
      font-family: 'SF Mono', 'Monaco', 'Consolas', monospace;
      letter-spacing: 0.5px;
    }

    .btn-icon {
      background: transparent;
      border: none;
      color: #888;
      cursor: pointer;
      padding: 0.125rem;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: color 0.2s ease;
    }

    .btn-icon:hover {
      color: #1a1a2e;
    }

    .btn-icon i {
      font-size: 0.85rem;
    }

    .btn-copy {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.625rem 1rem;
      background: #1a1a2e;
      color: white;
      border: none;
      border-radius: 10px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
      white-space: nowrap;
      flex-shrink: 0;
    }

    .btn-copy:hover {
      background: #2d2d44;
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(26, 26, 46, 0.3);
    }

    .btn-copy:active {
      transform: translateY(0);
    }

    .btn-copy.copied {
      background: #10b981;
    }

    .btn-copy i {
      font-size: 0.9rem;
    }

    /* Mobile: stack vertically */
    @media (max-width: 360px) {
      .wifi-card {
        flex-direction: column;
        align-items: stretch;
        gap: 0.75rem;
      }

      .btn-copy {
        justify-content: center;
      }
    }
  `]
})
export class WifiCardComponent implements OnInit {
  private apiService = inject(GuestApiService);

  wifi = signal<WifiCredentials | null>(null);
  showPassword = signal(false);
  copied = signal(false);

  ngOnInit(): void {
    this.loadWifi();
  }

  loadWifi(): void {
    this.apiService.getWifiCredentials().subscribe({
      next: (data) => {
        this.wifi.set(data);
      },
      error: (error) => {
        console.error('Failed to load WiFi credentials:', error);
      }
    });
  }

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  async copyPassword(): Promise<void> {
    const password = this.wifi()?.password;
    if (!password) return;

    try {
      await navigator.clipboard.writeText(password);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch (err) {
      console.error('Failed to copy password:', err);
    }
  }
}
