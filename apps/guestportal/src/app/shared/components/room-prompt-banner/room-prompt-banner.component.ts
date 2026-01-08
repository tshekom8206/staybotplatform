import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { RoomContextService } from '../../../core/services/room-context.service';

@Component({
  selector: 'app-room-prompt-banner',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  template: `
    @if (showBanner) {
      <div class="room-prompt-banner" [class.error]="errorMessage">
        <div class="banner-content">
          <i class="bi bi-door-open banner-icon"></i>
          @if (errorMessage) {
            <span class="banner-text error-text">{{ errorMessage }}</span>
          } @else {
            <span class="banner-text">{{ 'roomPrompt.message' | translate }}</span>
          }
          <div class="banner-input">
            <input
              type="text"
              [(ngModel)]="roomNumber"
              [placeholder]="'roomPrompt.placeholder' | translate"
              (keyup.enter)="saveRoom()"
              (input)="clearError()"
              maxlength="10"
              class="room-input"
              [class.input-error]="errorMessage"
              [disabled]="isValidating"
            />
            <button
              class="btn-save"
              (click)="saveRoom()"
              [disabled]="!roomNumber.trim() || isValidating"
              [attr.aria-label]="'common.save' | translate"
            >
              @if (isValidating) {
                <i class="bi bi-hourglass-split"></i>
              } @else {
                <i class="bi bi-check-lg"></i>
              }
            </button>
          </div>
          <button
            class="btn-dismiss"
            (click)="dismiss()"
            [attr.aria-label]="'common.close' | translate"
          >
            <i class="bi bi-x"></i>
          </button>
        </div>
      </div>
    }
  `,
  styles: [`
    .room-prompt-banner {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      padding: 0.75rem 1rem;
      position: sticky;
      top: 60px;
      z-index: 100;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
    }

    .banner-content {
      display: flex;
      align-items: center;
      justify-content: center;
      flex-wrap: wrap;
      gap: 0.5rem 0.75rem;
      max-width: 600px;
      margin: 0 auto;
    }

    .banner-icon {
      font-size: 1.25rem;
      flex-shrink: 0;
    }

    .banner-text {
      font-size: 0.9rem;
    }

    .banner-input {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }

    .room-input {
      width: 80px;
      padding: 0.4rem 0.75rem;
      border: none;
      border-radius: 20px;
      font-size: 0.9rem;
      background: rgba(255, 255, 255, 0.95);
      color: #333;
    }

    .room-input::placeholder {
      color: #888;
    }

    .room-input:focus {
      outline: none;
      box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.5);
    }

    .btn-save {
      background: rgba(255, 255, 255, 0.25);
      border: none;
      border-radius: 50%;
      width: 32px;
      height: 32px;
      color: white;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background 0.2s ease;
    }

    .btn-save:hover:not(:disabled) {
      background: rgba(255, 255, 255, 0.35);
    }

    .btn-save:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .btn-dismiss {
      background: rgba(255, 255, 255, 0.15);
      border: none;
      border-radius: 50%;
      width: 28px;
      height: 28px;
      color: white;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background 0.2s ease;
    }

    .btn-dismiss:hover {
      background: rgba(255, 255, 255, 0.25);
    }

    /* Error styles */
    .room-prompt-banner.error {
      background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
    }

    .error-text {
      color: #fff;
      font-weight: 500;
    }

    .input-error {
      border: 2px solid #e74c3c !important;
      background: #fff5f5 !important;
    }

    /* Mobile adjustments */
    @media (max-width: 480px) {
      .banner-icon {
        display: none;
      }

      .banner-text {
        font-size: 0.85rem;
        text-align: center;
      }

      .room-input {
        width: 70px;
        padding: 0.35rem 0.6rem;
        font-size: 0.85rem;
      }

      .btn-save {
        width: 28px;
        height: 28px;
      }

      .btn-dismiss {
        width: 24px;
        height: 24px;
      }
    }
  `]
})
export class RoomPromptBannerComponent implements OnInit, OnDestroy {
  private readonly DISMISS_KEY = 'room_banner_dismissed';
  private roomContext = inject(RoomContextService);
  private destroy$ = new Subject<void>();

  showBanner = false;
  roomNumber = '';
  errorMessage = '';
  isValidating = false;

  ngOnInit(): void {
    this.roomContext.roomNumber$
      .pipe(takeUntil(this.destroy$))
      .subscribe(room => {
        const dismissed = sessionStorage.getItem(this.DISMISS_KEY) === 'true';
        this.showBanner = !room && !dismissed;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  async saveRoom(): Promise<void> {
    const trimmed = this.roomNumber.trim();
    if (!trimmed || this.isValidating) {
      return;
    }

    this.isValidating = true;
    this.errorMessage = '';

    try {
      const result = await this.roomContext.setRoomNumberWithValidation(trimmed);

      if (result.valid) {
        this.showBanner = false;
      } else {
        this.errorMessage = result.error || 'Invalid room number';
      }
    } catch (error) {
      this.errorMessage = 'Failed to validate room';
    } finally {
      this.isValidating = false;
    }
  }

  clearError(): void {
    this.errorMessage = '';
  }

  dismiss(): void {
    sessionStorage.setItem(this.DISMISS_KEY, 'true');
    this.showBanner = false;
  }
}
