import { Component, EventEmitter, Input, Output, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { RoomContextService } from '../../../core/services/room-context.service';

@Component({
  selector: 'app-room-edit-sheet',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  template: `
    <!-- Bottom Sheet Overlay -->
    @if (isOpen) {
      <div class="sheet-overlay" (click)="close()">
        <div class="sheet-container" (click)="$event.stopPropagation()">
          <!-- Drag Handle -->
          <div class="drag-handle"></div>

          <!-- Header -->
          <div class="sheet-header">
            <div class="room-icon">
              <i class="bi bi-door-open"></i>
            </div>
            <h3>{{ 'room.edit' | translate }}</h3>
            <button class="btn-close-sheet" (click)="close()" aria-label="Close">
              <i class="bi bi-x-lg"></i>
            </button>
          </div>

          <!-- Content -->
          <div class="sheet-content">
            <div class="form-field">
              <label>{{ 'room.label' | translate }}</label>
              <input
                type="text"
                [(ngModel)]="newRoom"
                [placeholder]="currentRoom"
                (keyup.enter)="updateRoom()"
                (input)="clearError()"
                maxlength="10"
                class="room-input"
                [class.input-error]="error()"
                [disabled]="isValidating()"
              />
            </div>

            @if (error()) {
              <div class="error-message">
                <i class="bi bi-exclamation-circle"></i>
                {{ error() }}
              </div>
            }

            <button
              class="btn-update"
              (click)="updateRoom()"
              [disabled]="!newRoom.trim() || isValidating()"
              [class.loading]="isValidating()"
            >
              @if (isValidating()) {
                <i class="bi bi-hourglass-split spinning"></i>
                <span>{{ 'room.validating' | translate }}</span>
              } @else {
                <i class="bi bi-check2"></i>
                <span>{{ 'room.update' | translate }}</span>
              }
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
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

    .sheet-header .room-icon {
      width: 44px;
      height: 44px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .sheet-header .room-icon i {
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

    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .form-field label {
      font-size: 0.85rem;
      font-weight: 600;
      color: #666;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .room-input {
      width: 100%;
      padding: 1rem;
      font-size: 1.25rem;
      font-weight: 600;
      border: 2px solid #e0e0e0;
      border-radius: 12px;
      background: #f8f9fa;
      color: #1a1a2e;
      text-align: center;
      transition: border-color 0.2s ease, background 0.2s ease;
    }

    .room-input:focus {
      outline: none;
      border-color: #667eea;
      background: white;
    }

    .room-input::placeholder {
      color: #aaa;
    }

    .room-input.input-error {
      border-color: #e74c3c;
      background: #fff5f5;
    }

    .room-input:disabled {
      opacity: 0.7;
      cursor: not-allowed;
    }

    /* Error Message */
    .error-message {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.75rem 1rem;
      background: #fff5f5;
      border: 1px solid #ffcdd2;
      border-radius: 8px;
      color: #c62828;
      font-size: 0.9rem;
    }

    .error-message i {
      font-size: 1rem;
    }

    /* Update Button */
    .btn-update {
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

    .btn-update:hover:not(:disabled) {
      background: #2d2d44;
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(26, 26, 46, 0.3);
    }

    .btn-update:active:not(:disabled) {
      transform: translateY(0);
    }

    .btn-update:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-update.loading {
      background: #667eea;
    }

    .btn-update i {
      font-size: 1.1rem;
    }

    .spinning {
      animation: spin 1s linear infinite;
    }

    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
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
export class RoomEditSheetComponent {
  private roomContext = inject(RoomContextService);

  @Input() isOpen = false;
  @Input() currentRoom = '';
  @Output() closed = new EventEmitter<void>();
  @Output() roomChanged = new EventEmitter<string>();

  newRoom = '';
  error = signal('');
  isValidating = signal(false);

  constructor() {
    // Pre-fill with current room when sheet opens
    effect(() => {
      if (this.isOpen && this.currentRoom) {
        this.newRoom = this.currentRoom;
        this.error.set('');
      }
    });
  }

  close(): void {
    this.closed.emit();
    this.error.set('');
    document.body.style.overflow = '';
  }

  clearError(): void {
    this.error.set('');
  }

  async updateRoom(): Promise<void> {
    const trimmed = this.newRoom.trim();
    if (!trimmed || this.isValidating()) {
      return;
    }

    // If same room, just close
    if (trimmed === this.currentRoom) {
      this.close();
      return;
    }

    this.isValidating.set(true);
    this.error.set('');

    try {
      const result = await this.roomContext.setRoomNumberWithValidation(trimmed);

      if (result.valid) {
        this.roomChanged.emit(trimmed);
        this.close();
      } else {
        this.error.set(result.error || 'Invalid room number');
      }
    } catch (err) {
      this.error.set('Failed to validate room');
    } finally {
      this.isValidating.set(false);
    }
  }
}
