import { Component, Output, EventEmitter, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-custom-request-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  template: `
    @if (isOpen()) {
      <div class="modal-overlay" (click)="close()">
        <div class="modal-container" (click)="$event.stopPropagation()">
          <button class="btn-close" (click)="close()">
            <i class="bi bi-x-lg"></i>
          </button>

          <div class="modal-header">
            <i class="bi bi-chat-dots-fill"></i>
            <h2>{{ 'customRequest.title' | translate }}</h2>
            <p>{{ 'customRequest.subtitle' | translate }}</p>
          </div>

          <div class="modal-body">
            <label class="input-label">
              {{ 'customRequest.whatDoYouNeed' | translate }}
            </label>
            <textarea
              class="custom-request-textarea"
              [(ngModel)]="requestText"
              [placeholder]="'customRequest.placeholder' | translate"
              rows="6"
              maxlength="500"
            ></textarea>
            <div class="char-count">
              {{ requestText().length }}/500
            </div>

            @if (showTiming) {
              <label class="input-label">
                {{ 'customRequest.whenNeeded' | translate }}
              </label>
              <div class="timing-options">
                @for (option of timingOptions; track option.value) {
                  <label class="radio-option">
                    <input
                      type="radio"
                      name="timing"
                      [value]="option.value"
                      [(ngModel)]="selectedTiming"
                    />
                    <span>{{ option.label | translate }}</span>
                  </label>
                }
              </div>
            }
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" (click)="close()">
              {{ 'common.cancel' | translate }}
            </button>
            <button
              class="btn btn-primary"
              (click)="submit()"
              [disabled]="!requestText().trim() || loading()"
            >
              @if (loading()) {
                <span class="spinner"></span>
              }
              {{ 'common.submit' | translate }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    /* MATCHED TO EXISTING DESIGN SYSTEM - service-request-modal.component.ts */
    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.7);
      backdrop-filter: blur(8px);
      -webkit-backdrop-filter: blur(8px);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      animation: fadeIn 0.2s ease;
    }

    .modal-container {
      background: white;
      border-radius: 12px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      max-width: 500px;
      width: 100%;
      max-height: 90vh;
      overflow-y: auto;
      position: relative;
      animation: slideUp 0.3s ease;
    }

    .btn-close {
      position: absolute;
      top: 1rem;
      right: 1rem;
      background: transparent;
      border: none;
      font-size: 1.5rem;
      cursor: pointer;
      color: #888;
      padding: 0.5rem;
      transition: color 0.2s ease;
    }

    .btn-close:hover {
      color: #333;
    }

    .modal-header {
      padding: 2rem 2rem 1rem;
      text-align: center;
    }

    .modal-header i {
      font-size: 3rem;
      color: #333;
      margin-bottom: 1rem;
    }

    .modal-header h2 {
      font-size: 1.5rem;
      font-weight: 600;
      margin-bottom: 0.5rem;
      color: #1a1a1a;
    }

    .modal-header p {
      color: #666;
      font-size: 0.95rem;
    }

    .modal-body {
      padding: 0 2rem 1rem;
    }

    .input-label {
      display: block;
      font-weight: 600;
      margin-bottom: 0.5rem;
      color: #1a1a1a;
      font-size: 1rem;
    }

    .custom-request-textarea {
      width: 100%;
      padding: 1rem;
      border: 2px solid #e0e0e0;
      border-radius: 12px;
      font-size: 1rem;
      font-family: inherit;
      resize: vertical;
      transition: border-color 0.2s ease;
    }

    .custom-request-textarea:focus {
      outline: none;
      border-color: #333;
    }

    .char-count {
      text-align: right;
      font-size: 0.85rem;
      color: #888;
      margin-top: 0.25rem;
    }

    .timing-options {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-top: 0.75rem;
    }

    .radio-option {
      display: flex;
      align-items: center;
      padding: 1rem;
      background: #f8f9fa;
      border-radius: 12px;
      cursor: pointer;
      transition: background 0.2s ease;
    }

    .radio-option:hover {
      background: #e9ecef;
    }

    .radio-option input {
      margin-right: 0.75rem;
      cursor: pointer;
    }

    .modal-footer {
      padding: 1rem 2rem 2rem;
      display: flex;
      gap: 1rem;
    }

    .btn {
      flex: 1;
      padding: 0.75rem 1.25rem;
      border-radius: 50px;
      font-weight: 500;
      font-size: 0.95rem;
      cursor: pointer;
      transition: all 0.2s ease;
      border: none;
    }

    .btn-secondary {
      background: #f8f9fa;
      color: #666;
    }

    .btn-secondary:hover {
      background: #e9ecef;
    }

    .btn-primary {
      background: #333;
      color: white;
    }

    .btn-primary:hover:not(:disabled) {
      background: #1a1a1a;
      transform: translateY(-1px);
      box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    }

    .btn-primary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .spinner {
      display: inline-block;
      width: 1rem;
      height: 1rem;
      border: 2px solid rgba(255,255,255,0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
      margin-right: 0.5rem;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes slideUp {
      from { transform: translateY(20px); opacity: 0; }
      to { transform: translateY(0); opacity: 1; }
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class CustomRequestModalComponent {
  @Input() isOpen = signal(false);
  @Input() showTiming = true; // For prepare page
  @Input() department = 'General'; // For routing
  @Output() closed = new EventEmitter<void>();
  @Output() submitted = new EventEmitter<{request: string; timing?: string}>();

  requestText = signal('');
  selectedTiming = signal('asap');
  loading = signal(false);

  timingOptions = [
    { value: 'before-arrival', label: 'customRequest.beforeArrival' },
    { value: 'check-in', label: 'customRequest.uponCheckIn' },
    { value: 'later', label: 'customRequest.laterDuringStay' }
  ];

  close(): void {
    this.isOpen.set(false);
    this.requestText.set('');
    this.selectedTiming.set('asap');
    this.closed.emit();
  }

  submit(): void {
    const text = this.requestText().trim();
    if (!text) return;

    this.loading.set(true);
    this.submitted.emit({
      request: text,
      timing: this.showTiming ? this.selectedTiming() : undefined
    });
  }

  resetLoading(): void {
    this.loading.set(false);
  }
}
