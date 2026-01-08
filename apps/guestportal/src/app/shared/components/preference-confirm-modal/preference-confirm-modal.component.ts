import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';

export interface PendingPreference {
  type: string;
  value: any;
  notes?: string;
  label: string;
  description: string;
  icon: string;
  previousValue?: any;
}

@Component({
  selector: 'app-preference-confirm-modal',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    @if (isOpen && preference) {
      <div class="modal-backdrop" (click)="cancel()"></div>
      <div class="modal-container">
        <div class="modal-content">
          <button class="close-btn" (click)="cancel()">
            <i class="bi bi-x-lg"></i>
          </button>

          <!-- Preference Header -->
          <div class="preference-header">
            <div class="preference-icon-wrapper">
              <i class="bi" [ngClass]="preference.icon"></i>
            </div>
            <div class="preference-info">
              <h3 class="preference-name">{{ preference.label }}</h3>
            </div>
          </div>

          <div class="divider"></div>

          <!-- Confirmation Message -->
          <div class="confirmation-body">
            <p class="confirmation-message">{{ preference.description }}</p>
          </div>

          <!-- Action Buttons -->
          <div class="modal-footer">
            <button class="cancel-btn" (click)="cancel()">
              <i class="bi bi-x"></i>
              <span>{{ 'housekeeping.cancelButton' | translate }}</span>
            </button>
            <button class="confirm-btn" (click)="confirm()">
              <i class="bi bi-check-lg"></i>
              <span>{{ 'housekeeping.confirmButton' | translate }}</span>
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.7);
      backdrop-filter: blur(8px);
      -webkit-backdrop-filter: blur(8px);
      z-index: 1000;
    }

    .modal-container {
      position: fixed;
      left: 50%;
      bottom: 0;
      transform: translateX(-50%);
      width: 100%;
      max-width: 420px;
      z-index: 1001;
      animation: slideUp 0.35s cubic-bezier(0.32, 0.72, 0, 1);
    }

    @keyframes slideUp {
      from {
        transform: translateX(-50%) translateY(100%);
      }
      to {
        transform: translateX(-50%) translateY(0);
      }
    }

    .modal-content {
      background: linear-gradient(180deg, #2a2a2a 0%, #1a1a1a 100%);
      border-radius: 28px 28px 0 0;
      padding: 1.75rem 1.5rem 2rem;
      position: relative;
      box-shadow: 0 -10px 40px rgba(0, 0, 0, 0.4);
    }

    .close-btn {
      position: absolute;
      top: 1rem;
      right: 1rem;
      background: rgba(255, 255, 255, 0.08);
      border: none;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      color: rgba(255, 255, 255, 0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: all 0.2s;
      font-size: 0.9rem;
    }

    .close-btn:hover {
      background: rgba(255, 255, 255, 0.15);
      color: white;
    }

    /* Preference Header */
    .preference-header {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding-right: 2.5rem;
    }

    .preference-icon-wrapper {
      width: 56px;
      height: 56px;
      border-radius: 16px;
      background: linear-gradient(135deg, rgba(255, 255, 255, 0.15) 0%, rgba(255, 255, 255, 0.05) 100%);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      border: 1px solid rgba(255, 255, 255, 0.1);
    }

    .preference-icon-wrapper i {
      font-size: 1.5rem;
      color: white;
    }

    .preference-info {
      flex: 1;
      min-width: 0;
    }

    .preference-name {
      color: white;
      font-size: 1.15rem;
      font-weight: 600;
      margin: 0;
      line-height: 1.3;
    }

    .divider {
      height: 1px;
      background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.1), transparent);
      margin: 1.25rem 0;
    }

    .confirmation-body {
      margin-bottom: 1.5rem;
    }

    .confirmation-message {
      color: rgba(255, 255, 255, 0.8);
      font-size: 1rem;
      line-height: 1.6;
      margin: 0;
      text-align: center;
    }

    /* Action Buttons */
    .modal-footer {
      display: flex;
      gap: 0.75rem;
    }

    .cancel-btn, .confirm-btn {
      flex: 1;
      border: none;
      border-radius: 14px;
      padding: 1rem 1.5rem;
      font-size: 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }

    .cancel-btn {
      background: rgba(255, 255, 255, 0.1);
      color: white;
      border: 1px solid rgba(255, 255, 255, 0.2);
    }

    .cancel-btn:hover {
      background: rgba(255, 255, 255, 0.15);
      border-color: rgba(255, 255, 255, 0.3);
    }

    .confirm-btn {
      background: linear-gradient(135deg, #4CAF50 0%, #2E7D32 100%);
      color: white;
      box-shadow: 0 4px 12px rgba(76, 175, 80, 0.3);
    }

    .confirm-btn:hover {
      transform: translateY(-1px);
      box-shadow: 0 6px 16px rgba(76, 175, 80, 0.4);
    }

    .confirm-btn:active {
      transform: translateY(0);
    }

    /* Desktop Styles */
    @media (min-width: 576px) {
      .modal-container {
        bottom: auto;
        top: 50%;
        transform: translate(-50%, -50%);
        animation: fadeIn 0.3s ease;
      }

      @keyframes fadeIn {
        from {
          opacity: 0;
          transform: translate(-50%, -50%) scale(0.95);
        }
        to {
          opacity: 1;
          transform: translate(-50%, -50%) scale(1);
        }
      }

      .modal-content {
        border-radius: 24px;
        padding: 2rem;
      }
    }
  `]
})
export class PreferenceConfirmModalComponent {
  @Input() isOpen = false;
  @Input() preference: PendingPreference | null = null;
  @Output() confirmed = new EventEmitter<PendingPreference>();
  @Output() cancelled = new EventEmitter<void>();

  confirm(): void {
    if (this.preference) {
      this.confirmed.emit(this.preference);
    }
  }

  cancel(): void {
    this.cancelled.emit();
  }
}
