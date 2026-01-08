import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, WifiCredentials } from '../../../core/services/guest-api.service';

@Component({
  selector: 'app-wifi-bottom-sheet',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    <!-- Floating WiFi Pill Button -->
    @if (wifi() && wifi()!.network) {
      <button class="wifi-pill" (click)="open()" [attr.aria-label]="'wifi.title' | translate">
        <i class="bi bi-wifi"></i>
        <span>{{ 'wifi.button' | translate }}</span>
      </button>
    }

    <!-- Bottom Sheet Overlay -->
    @if (isOpen()) {
      <div class="sheet-overlay" (click)="close()">
        <div class="sheet-container" (click)="$event.stopPropagation()">
          <!-- Drag Handle -->
          <div class="drag-handle"></div>

          <!-- Header -->
          <div class="sheet-header">
            <div class="wifi-icon">
              <i class="bi bi-wifi"></i>
            </div>
            <h3>{{ 'wifi.title' | translate }}</h3>
            <button class="btn-close-sheet" (click)="close()" aria-label="Close">
              <i class="bi bi-x-lg"></i>
            </button>
          </div>

          <!-- Content -->
          <div class="sheet-content">
            <div class="wifi-field">
              <label>{{ 'wifi.network' | translate }}</label>
              <div class="field-value">{{ wifi()!.network }}</div>
            </div>

            <div class="wifi-field">
              <label>{{ 'wifi.password' | translate }}</label>
              <div class="field-value password-field">
                <span class="password-text">
                  {{ showPassword() ? wifi()!.password : '••••••••••' }}
                </span>
                <button class="btn-toggle" (click)="togglePassword()" [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'">
                  <i class="bi" [class.bi-eye]="!showPassword()" [class.bi-eye-slash]="showPassword()"></i>
                </button>
              </div>
            </div>

            <button class="btn-copy-large" (click)="copyPassword()" [class.copied]="copied()">
              <i class="bi" [class.bi-check2]="copied()" [class.bi-clipboard]="!copied()"></i>
              <span>{{ copied() ? ('wifi.copied' | translate) : ('wifi.copy' | translate) }}</span>
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    /* Floating WiFi Pill Button */
    .wifi-pill {
      position: absolute;
      top: 1rem;
      right: 1rem;
      padding: 0.6rem 1rem;
      border-radius: 50px;
      background: rgba(255, 255, 255, 0.95);
      border: none;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      transition: all 0.2s ease;
      z-index: 10;
    }

    .wifi-pill:hover {
      transform: scale(1.03);
      box-shadow: 0 6px 20px rgba(0, 0, 0, 0.2);
    }

    .wifi-pill:active {
      transform: scale(0.97);
    }

    .wifi-pill i {
      font-size: 1.1rem;
      color: #667eea;
    }

    .wifi-pill span {
      font-size: 0.9rem;
      font-weight: 600;
      color: #1a1a2e;
    }

    /* Sheet Overlay */
    .sheet-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
      z-index: 1000;
      display: flex;
      align-items: flex-end;
      justify-content: center;
      animation: fadeIn 0.2s ease-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    /* Sheet Container */
    .sheet-container {
      background: white;
      border-radius: 24px 24px 0 0;
      width: 100%;
      max-width: 480px;
      padding: 0.75rem 1.5rem 2rem;
      animation: slideUp 0.3s ease-out;
      max-height: 90vh;
      overflow-y: auto;
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

    /* Drag Handle */
    .drag-handle {
      width: 40px;
      height: 4px;
      background: #ddd;
      border-radius: 2px;
      margin: 0 auto 1rem;
    }

    /* Header */
    .sheet-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1.5rem;
    }

    .sheet-header .wifi-icon {
      width: 44px;
      height: 44px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .sheet-header .wifi-icon i {
      font-size: 1.25rem;
      color: white;
    }

    .sheet-header h3 {
      flex: 1;
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a1a2e;
      margin: 0;
    }

    .btn-close-sheet {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: #f5f5f5;
      border: none;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background 0.2s ease;
    }

    .btn-close-sheet:hover {
      background: #eee;
    }

    .btn-close-sheet i {
      font-size: 1rem;
      color: #666;
    }

    /* Content */
    .sheet-content {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .wifi-field {
      background: #f8f9fa;
      border-radius: 12px;
      padding: 1rem;
    }

    .wifi-field label {
      display: block;
      font-size: 0.75rem;
      font-weight: 600;
      color: #888;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 0.375rem;
    }

    .field-value {
      font-size: 1.1rem;
      font-weight: 500;
      color: #1a1a2e;
    }

    .password-field {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .password-text {
      flex: 1;
      font-family: 'SF Mono', 'Monaco', 'Consolas', monospace;
      letter-spacing: 1px;
    }

    .btn-toggle {
      background: transparent;
      border: none;
      color: #888;
      cursor: pointer;
      padding: 0.25rem;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: color 0.2s ease;
    }

    .btn-toggle:hover {
      color: #1a1a2e;
    }

    .btn-toggle i {
      font-size: 1.1rem;
    }

    /* Copy Button */
    .btn-copy-large {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 1rem;
      background: #1a1a2e;
      color: white;
      border: none;
      border-radius: 12px;
      font-size: 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
      margin-top: 0.5rem;
    }

    .btn-copy-large:hover {
      background: #2d2d44;
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(26, 26, 46, 0.3);
    }

    .btn-copy-large:active {
      transform: translateY(0);
    }

    .btn-copy-large.copied {
      background: #10b981;
    }

    .btn-copy-large i {
      font-size: 1.1rem;
    }

    /* Desktop adjustments */
    @media (min-width: 481px) {
      .sheet-container {
        border-radius: 24px;
        margin-bottom: 2rem;
      }
    }
  `]
})
export class WifiBottomSheetComponent implements OnInit {
  private apiService = inject(GuestApiService);

  wifi = signal<WifiCredentials | null>(null);
  isOpen = signal(false);
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

  open(): void {
    this.isOpen.set(true);
    document.body.style.overflow = 'hidden';
  }

  close(): void {
    this.isOpen.set(false);
    document.body.style.overflow = '';
    this.showPassword.set(false);
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
