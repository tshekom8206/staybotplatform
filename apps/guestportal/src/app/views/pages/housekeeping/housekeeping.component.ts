import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { RoomPreferencesService, RoomPreference, CreateRoomPreferenceRequest } from '../../../core/services/room-preferences.service';
import { RoomContextService } from '../../../core/services/room-context.service';
import { GuestApiService, RequestItem } from '../../../core/services/guest-api.service';
import { PreferenceConfirmModalComponent, PendingPreference } from '../../../shared/components/preference-confirm-modal/preference-confirm-modal.component';

interface PreferenceToggle {
  type: string;
  labelKey: string;
  icon: string;
  enabled: boolean;
  description?: string;
}

@Component({
  selector: 'app-housekeeping',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslateModule, PreferenceConfirmModalComponent],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'housekeeping.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'housekeeping.subtitle' | translate }}</p>
        </div>

        @if (loading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else {
          <!-- Temperature Preferences -->
          <div class="preference-section">
            <h3 class="section-title">
              <i class="bi bi-thermometer-half"></i>
              {{ 'housekeeping.temperature' | translate }}
            </h3>
            
            <div class="preference-card">
              <div class="preference-item">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.airconAfterCleaning' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.airconDescription' | translate }}</p>
                </div>
                <div class="form-check form-switch">
                  <input class="form-check-input"
                         type="checkbox"
                         role="switch"
                         [(ngModel)]="airconEnabled"
                         (change)="requestAirconPreference()">
                </div>
              </div>
              
              @if (!airconEnabled) {
                <div class="preference-note">
                  <textarea class="form-control form-control-sm"
                            [(ngModel)]="airconNote"
                            [placeholder]="'housekeeping.addNote' | translate"
                            rows="2"></textarea>
                  <button class="btn btn-sm btn-outline-primary mt-2" 
                          (click)="savePreference('aircon_after_cleaning', { enabled: airconEnabled }, airconNote)">
                    {{ 'common.save' | translate }}
                  </button>
                </div>
              }
            </div>
          </div>

          <!-- Cleaning Preferences -->
          <div class="preference-section">
            <h3 class="section-title">
              <i class="bi bi-droplet"></i>
              {{ 'housekeeping.cleaning' | translate }}
            </h3>
            
            <div class="preference-card">
              <div class="preference-item">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.dailyLinenChange' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.linenDescription' | translate }}</p>
                </div>
                <div class="form-check form-switch">
                  <input class="form-check-input"
                         type="checkbox"
                         role="switch"
                         [(ngModel)]="dailyLinenChange"
                         (change)="requestLinenPreference()">
                </div>
              </div>

              <div class="preference-item">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.dailyTowelChange' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.towelDescription' | translate }}</p>
                </div>
                <div class="form-check form-switch">
                  <input class="form-check-input"
                         type="checkbox"
                         role="switch"
                         [(ngModel)]="dailyTowelChange"
                         (change)="requestTowelPreference()">
                </div>
              </div>
            </div>
          </div>

          <!-- Do Not Disturb -->
          <div class="preference-section">
            <h3 class="section-title">
              <i class="bi bi-moon"></i>
              {{ 'housekeeping.doNotDisturb' | translate }}
            </h3>
            
            <div class="preference-card">
              <div class="preference-item">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.dndEnabled' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.dndDescription' | translate }}</p>
                </div>
                <div class="form-check form-switch">
                  <input class="form-check-input"
                         type="checkbox"
                         role="switch"
                         [(ngModel)]="dndEnabled"
                         (change)="requestDndPreference()">
                </div>
              </div>

              @if (dndEnabled) {
                <div class="preference-note">
                  <label class="form-label">{{ 'housekeeping.dndUntil' | translate }}</label>
                  <input type="time"
                         class="form-control"
                         [(ngModel)]="dndUntilTime"
                         (change)="requestDndTimePreference()">
                </div>
              }
            </div>
          </div>

          <!-- Cleaning Schedule -->
          <div class="preference-section">
            <h3 class="section-title">
              <i class="bi bi-clock"></i>
              {{ 'housekeeping.cleaningTime' | translate }}
            </h3>

            <div class="preference-card">
              <div class="preference-item" style="border-bottom: none;">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.preferredTime' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.preferredTimeDescription' | translate }}</p>
                </div>
              </div>
              <div class="time-options">
                <button class="btn time-pill"
                        [class.active]="cleaningTime === 'morning'"
                        (click)="requestCleaningTime('morning')">
                  {{ 'housekeeping.morning' | translate }}
                </button>
                <button class="btn time-pill"
                        [class.active]="cleaningTime === 'afternoon'"
                        (click)="requestCleaningTime('afternoon')">
                  {{ 'housekeeping.afternoon' | translate }}
                </button>
                <button class="btn time-pill"
                        [class.active]="cleaningTime === 'skip'"
                        (click)="requestCleaningTime('skip')">
                  {{ 'housekeeping.skipToday' | translate }}
                </button>
              </div>
            </div>
          </div>

          <!-- Bedding & Comfort -->
          <div class="preference-section">
            <h3 class="section-title">
              <i class="bi bi-house-heart"></i>
              {{ 'housekeeping.beddingComfort' | translate }}
            </h3>

            <div class="preference-card">
              <!-- Extra Pillows -->
              <div class="preference-item">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.extraPillows' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.extraPillowsDescription' | translate }}</p>
                </div>
                <div class="quantity-control">
                  <button class="btn btn-sm btn-outline-secondary"
                          (click)="requestDecrementPillows()" [disabled]="extraPillows <= 0">-</button>
                  <span class="quantity-value">{{ extraPillows }}</span>
                  <button class="btn btn-sm btn-outline-secondary"
                          (click)="requestIncrementPillows()" [disabled]="extraPillows >= 4">+</button>
                </div>
              </div>

              <!-- Extra Blankets -->
              <div class="preference-item">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.extraBlankets' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.extraBlanketsDescription' | translate }}</p>
                </div>
                <div class="quantity-control">
                  <button class="btn btn-sm btn-outline-secondary"
                          (click)="requestDecrementBlankets()" [disabled]="extraBlankets <= 0">-</button>
                  <span class="quantity-value">{{ extraBlankets }}</span>
                  <button class="btn btn-sm btn-outline-secondary"
                          (click)="requestIncrementBlankets()" [disabled]="extraBlankets >= 3">+</button>
                </div>
              </div>

              <!-- Pillow Type -->
              <div class="preference-item" style="border-bottom: none; padding-bottom: 0.5rem;">
                <div class="preference-info">
                  <label class="preference-label">{{ 'housekeeping.pillowType' | translate }}</label>
                  <p class="preference-description">{{ 'housekeeping.pillowTypeDescription' | translate }}</p>
                </div>
              </div>
              <div class="pillow-options">
                <button class="btn pillow-pill"
                        [class.active]="pillowType === 'soft'"
                        (click)="requestPillowType('soft')">
                  {{ 'housekeeping.pillowSoft' | translate }}
                </button>
                <button class="btn pillow-pill"
                        [class.active]="pillowType === 'medium'"
                        (click)="requestPillowType('medium')">
                  {{ 'housekeeping.pillowMedium' | translate }}
                </button>
                <button class="btn pillow-pill"
                        [class.active]="pillowType === 'firm'"
                        (click)="requestPillowType('firm')">
                  {{ 'housekeeping.pillowFirm' | translate }}
                </button>
                <button class="btn pillow-pill"
                        [class.active]="pillowType === 'hypoallergenic'"
                        (click)="requestPillowType('hypoallergenic')">
                  {{ 'housekeeping.pillowHypoallergenic' | translate }}
                </button>
              </div>
            </div>
          </div>

          <!-- Need Something? - Request Items -->
          @if (availableItems().length > 0) {
            <div class="preference-section">
              <h3 class="section-title">
                <i class="bi bi-box-seam"></i>
                {{ 'housekeeping.needSomething' | translate }}
              </h3>

              <div class="preference-card">
                @for (item of availableItems(); track item.id; let last = $last) {
                  <div class="preference-item" [class.requested]="isItemRequested(item.id)" [style.border-bottom]="last ? 'none' : null">
                    <div class="preference-info">
                      <div class="item-with-icon">
                        <i class="bi item-icon" [ngClass]="item.icon"></i>
                        <div>
                          <label class="preference-label">{{ item.name }}</label>
                          <p class="preference-description">{{ 'housekeeping.deliverToRoom' | translate }}</p>
                        </div>
                      </div>
                    </div>
                    @if (isItemRequested(item.id)) {
                      <div class="requested-badge">
                        <i class="bi bi-check-circle-fill"></i>
                        {{ 'housekeeping.requested' | translate }}
                      </div>
                    } @else {
                      <button class="btn btn-sm request-btn"
                              (click)="requestItem(item)"
                              [disabled]="requestingItem() === item.id">
                        @if (requestingItem() === item.id) {
                          <span class="spinner-border spinner-border-sm"></span>
                        } @else {
                          {{ 'housekeeping.request' | translate }}
                        }
                      </button>
                    }
                  </div>
                }
              </div>
            </div>
          }

          <!-- Active Preferences List -->
          @if (activePreferences().length > 0) {
            <div class="preference-section">
              <h3 class="section-title">
                <i class="bi bi-check-circle"></i>
                {{ 'housekeeping.activePreferences' | translate }}
              </h3>
              
              <div class="preferences-list">
                @for (pref of activePreferences(); track pref.id) {
                  <div class="preference-list-item">
                    <div class="preference-list-info">
                      <span class="preference-type">{{ getPreferenceLabel(pref.preferenceType) }}</span>
                      @if (pref.acknowledgedAt) {
                        <span class="badge bg-success">
                          <i class="bi bi-check"></i> {{ 'housekeeping.acknowledged' | translate }}
                        </span>
                      } @else {
                        <span class="badge bg-warning">
                          <i class="bi bi-clock"></i> {{ 'housekeeping.pending' | translate }}
                        </span>
                      }
                    </div>
                    <button class="btn btn-sm btn-outline-danger" 
                            (click)="cancelPreference(pref.id)">
                      <i class="bi bi-x"></i>
                    </button>
                  </div>
                }
              </div>
            </div>
          }

          <!-- Success Toast -->
          @if (showSuccessToast()) {
            <div class="toast-container position-fixed bottom-0 end-0 p-3">
              <div class="toast show" role="alert">
                <div class="toast-header bg-success text-white">
                  <i class="bi bi-check-circle me-2"></i>
                  <strong class="me-auto">{{ 'housekeeping.saved' | translate }}</strong>
                  <button type="button" class="btn-close btn-close-white" (click)="showSuccessToast.set(false)"></button>
                </div>
                <div class="toast-body">
                  {{ 'housekeeping.savedMessage' | translate }}
                </div>
              </div>
            </div>
          }

          <!-- Confirmation Modal -->
          <app-preference-confirm-modal
            [isOpen]="showConfirmModal()"
            [preference]="pendingPreference()"
            (confirmed)="onConfirmPreference($event)"
            (cancelled)="onCancelPreference()">
          </app-preference-confirm-modal>
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 1rem 0;
    }

    /* Page Header - Clean, floating on background */
    .page-header {
      padding: 1.5rem 0 1.25rem;
      margin-bottom: 1rem;
    }

    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-decoration: none;
      font-size: 0.9rem;
      font-weight: 500;
      padding: 0.4rem 0.75rem;
      margin: -0.4rem -0.75rem 0.75rem;
      border-radius: 50px;
      transition: all 0.2s ease;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .back-link:hover {
      background: rgba(255, 255, 255, 0.15);
      color: white;
    }

    .back-link i {
      font-size: 1rem;
    }

    .page-title {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }

    .page-subtitle {
      font-size: 0.95rem;
      color: rgba(255, 255, 255, 0.9);
      margin: 0.25rem 0 0;
      text-shadow: 0 1px 6px rgba(0, 0, 0, 0.3);
    }

    .preference-section {
      margin-bottom: 1.5rem;
    }

    .section-title {
      font-size: 1.1rem;
      font-weight: 600;
      margin-bottom: 0.75rem;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .section-title i {
      font-size: 1rem;
    }

    .preference-card {
      background: white;
      border-radius: 12px;
      padding: 1rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }

    .preference-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 0;
      border-bottom: 1px solid #e9ecef;
    }

    .preference-item:first-child {
      padding-top: 0;
    }

    .preference-item:last-child {
      border-bottom: none;
      padding-bottom: 0;
    }

    .preference-info {
      flex: 1;
      padding-right: 1rem;
    }

    .preference-label {
      font-size: 1rem;
      font-weight: 600;
      margin-bottom: 0.25rem;
      display: block;
      color: #1a1a1a;
    }

    .preference-description {
      font-size: 0.85rem;
      color: #666;
      margin: 0;
    }

    .form-check-input {
      width: 3rem;
      height: 1.5rem;
      cursor: pointer;
      background-color: #e9ecef;
      border-color: #adb5bd;
    }

    .form-check-input:checked {
      background-color: #333;
      border-color: #333;
    }

    .form-check-input:focus {
      border-color: #666;
      box-shadow: 0 0 0 0.25rem rgba(51, 51, 51, 0.25);
    }

    .preference-note {
      margin-top: 1rem;
      padding: 1rem;
      background: #f8f9fa;
      border-radius: 8px;
    }

    .preferences-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .preference-list-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem;
      background: white;
      border-radius: 12px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }

    .preference-list-info {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .preference-type {
      font-weight: 600;
      color: #1a1a1a;
    }

    .badge {
      font-size: 0.75rem;
      padding: 0.25rem 0.5rem;
    }

    .toast-container {
      z-index: 1050;
    }

    .time-options, .pillow-options {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      padding: 0 1rem 1rem;
    }

    .time-pill, .pillow-pill {
      flex: 1;
      min-width: 80px;
      padding: 0.5rem 0.75rem;
      border: 2px solid #e9ecef;
      border-radius: 50px;
      background: white;
      color: #333;
      font-size: 0.85rem;
      font-weight: 500;
      transition: all 0.2s ease;
    }

    .time-pill:hover, .pillow-pill:hover {
      border-color: #333;
    }

    .time-pill.active, .pillow-pill.active {
      background: #333;
      border-color: #333;
      color: white;
    }

    .quantity-control {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .quantity-control .btn {
      width: 32px;
      height: 32px;
      padding: 0;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
    }

    .quantity-value {
      font-size: 1.25rem;
      font-weight: 600;
      min-width: 2rem;
      text-align: center;
    }

    /* Request Items Styles */
    .item-with-icon {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .item-icon {
      font-size: 1.25rem;
      color: #333;
      width: 1.5rem;
      text-align: center;
    }

    .preference-item.requested {
      background: #f0fdf4;
      margin: 0 -1rem;
      padding-left: 1rem;
      padding-right: 1rem;
      border-radius: 8px;
    }

    .preference-item.requested .item-icon {
      color: #333;
    }

    .request-btn {
      padding: 0.4rem 1rem;
      border-radius: 50px;
      font-size: 0.85rem;
      font-weight: 500;
      min-width: 85px;
      background-color: #333;
      border-color: #333;
      color: white;
    }

    .request-btn:hover:not(:disabled) {
      background-color: #1a1a1a;
      border-color: #1a1a1a;
      color: white;
    }

    .request-btn:disabled {
      opacity: 0.6;
    }

    .requested-badge {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: #28a745;
      font-weight: 600;
      font-size: 0.85rem;
    }

    .requested-badge i {
      font-size: 1rem;
    }

    @media (max-width: 768px) {
      .page-title {
        font-size: 1.5rem;
      }

      .time-pill, .pillow-pill {
        min-width: 70px;
        font-size: 0.8rem;
        padding: 0.4rem 0.5rem;
      }

      .item-icon {
        font-size: 1.1rem;
      }

      .request-btn {
        padding: 0.35rem 0.75rem;
        font-size: 0.8rem;
        min-width: 75px;
      }
    }
  `]
})
export class HousekeepingComponent implements OnInit {
  private preferencesService = inject(RoomPreferencesService);
  private roomContext = inject(RoomContextService);
  private guestApi = inject(GuestApiService);
  private translate = inject(TranslateService);

  loading = signal(false);
  showSuccessToast = signal(false);
  activePreferences = signal<RoomPreference[]>([]);
  showConfirmModal = signal(false);
  pendingPreference = signal<PendingPreference | null>(null);

  // Request items state
  availableItems = signal<RequestItem[]>([]);
  requestedItems = signal<Set<number>>(new Set());
  requestingItem = signal<number | null>(null);

  // Store previous values for reverting on cancel
  private previousValues: Record<string, any> = {};

  // Preference states
  airconEnabled = true;
  airconNote = '';
  dailyLinenChange = true;
  dailyTowelChange = true;
  dndEnabled = false;
  dndUntilTime = '14:00';

  // New preference states
  cleaningTime = '';
  extraPillows = 0;
  extraBlankets = 0;
  pillowType = '';

  ngOnInit() {
    this.loadPreferences();
    this.loadRequestItems();
  }

  loadPreferences() {
    this.loading.set(true);
    this.preferencesService.getPreferences().subscribe({
      next: (preferences) => {
        this.activePreferences.set(preferences.filter(p => p.status === 'Active' || p.status === 'Acknowledged'));
        
        // Set toggle states from existing preferences
        preferences.forEach(pref => {
          const value = pref.preferenceValue;
          switch (pref.preferenceType) {
            case 'aircon_after_cleaning':
              this.airconEnabled = value.enabled ?? true;
              this.airconNote = pref.notes ?? '';
              break;
            case 'linen_change_frequency':
              this.dailyLinenChange = value.daily ?? true;
              break;
            case 'towel_change_frequency':
              this.dailyTowelChange = value.daily ?? true;
              break;
            case 'dnd_schedule':
              this.dndEnabled = true;
              this.dndUntilTime = value.until ?? '14:00';
              break;
            case 'cleaning_time':
              this.cleaningTime = value.time ?? '';
              break;
            case 'extra_pillows':
              this.extraPillows = value.count ?? 0;
              break;
            case 'extra_blankets':
              this.extraBlankets = value.count ?? 0;
              break;
            case 'pillow_type':
              this.pillowType = value.type ?? '';
              break;
          }
        });

        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load preferences:', err);
        this.loading.set(false);
      }
    });
  }

  savePreference(type: string, value: any, notes?: string) {
    const request: CreateRoomPreferenceRequest = {
      preferenceType: type,
      preferenceValue: value,
      notes: notes
    };

    this.preferencesService.createOrUpdatePreference(request).subscribe({
      next: () => {
        this.showSuccessToast.set(true);
        setTimeout(() => this.showSuccessToast.set(false), 3000);
        this.loadPreferences();
      },
      error: (err) => {
        console.error('Failed to save preference:', err);
        alert('Failed to save preference. Please try again.');
      }
    });
  }

  toggleDnd() {
    if (this.dndEnabled) {
      this.savePreference('dnd_schedule', { until: this.dndUntilTime });
    } else {
      // Find and cancel DND preference
      const dndPref = this.activePreferences().find(p => p.preferenceType === 'dnd_schedule');
      if (dndPref) {
        this.cancelPreference(dndPref.id);
      }
    }
  }

  cancelPreference(id: number) {
    if (!confirm('Are you sure you want to cancel this preference?')) {
      return;
    }

    this.preferencesService.cancelPreference(id).subscribe({
      next: () => {
        this.loadPreferences();
      },
      error: (err) => {
        console.error('Failed to cancel preference:', err);
        alert('Failed to cancel preference. Please try again.');
      }
    });
  }

  getPreferenceLabel(type: string): string {
    const labels: Record<string, string> = {
      'aircon_after_cleaning': 'Aircon after cleaning',
      'linen_change_frequency': 'Linen change frequency',
      'towel_change_frequency': 'Towel change frequency',
      'dnd_schedule': 'Do Not Disturb',
      'cleaning_time': 'Preferred cleaning time',
      'extra_pillows': 'Extra pillows',
      'extra_blankets': 'Extra blankets',
      'pillow_type': 'Pillow preference'
    };
    return labels[type] || type;
  }

  // New preference methods
  setCleaningTime(time: string) {
    this.cleaningTime = time;
    this.savePreference('cleaning_time', { time: time });
  }

  incrementPillows() {
    if (this.extraPillows < 4) {
      this.extraPillows++;
      this.savePreference('extra_pillows', { count: this.extraPillows });
    }
  }

  decrementPillows() {
    if (this.extraPillows > 0) {
      this.extraPillows--;
      if (this.extraPillows === 0) {
        // Find and cancel pillow preference when set to 0
        const pillowPref = this.activePreferences().find(p => p.preferenceType === 'extra_pillows');
        if (pillowPref) {
          this.preferencesService.cancelPreference(pillowPref.id).subscribe({
            next: () => this.loadPreferences()
          });
        }
      } else {
        this.savePreference('extra_pillows', { count: this.extraPillows });
      }
    }
  }

  setPillowType(type: string) {
    this.pillowType = type;
    this.savePreference('pillow_type', { type: type });
  }

  // ============================================
  // Confirmation Modal Methods
  // ============================================

  private showConfirmation(preference: PendingPreference): void {
    this.pendingPreference.set(preference);
    this.showConfirmModal.set(true);
  }

  onConfirmPreference(preference: PendingPreference): void {
    this.showConfirmModal.set(false);
    this.pendingPreference.set(null);

    // Actually save the preference
    if (preference.type === 'dnd_cancel') {
      // Special case: cancelling DND
      const dndPref = this.activePreferences().find(p => p.preferenceType === 'dnd_schedule');
      if (dndPref) {
        this.preferencesService.cancelPreference(dndPref.id).subscribe({
          next: () => this.loadPreferences()
        });
      }
    } else if (preference.type === 'extra_pillows_cancel') {
      // Special case: cancelling pillows (decrement to 0)
      const pillowPref = this.activePreferences().find(p => p.preferenceType === 'extra_pillows');
      if (pillowPref) {
        this.preferencesService.cancelPreference(pillowPref.id).subscribe({
          next: () => this.loadPreferences()
        });
      }
    } else if (preference.type === 'extra_blankets_cancel') {
      // Special case: cancelling blankets (decrement to 0)
      const blanketPref = this.activePreferences().find(p => p.preferenceType === 'extra_blankets');
      if (blanketPref) {
        this.preferencesService.cancelPreference(blanketPref.id).subscribe({
          next: () => this.loadPreferences()
        });
      }
    } else {
      this.savePreference(preference.type, preference.value, preference.notes);
    }
  }

  onCancelPreference(): void {
    this.showConfirmModal.set(false);
    const preference = this.pendingPreference();

    // Revert to previous value
    if (preference) {
      switch (preference.type) {
        case 'aircon_after_cleaning':
          this.airconEnabled = this.previousValues['airconEnabled'] ?? true;
          break;
        case 'linen_change_frequency':
          this.dailyLinenChange = this.previousValues['dailyLinenChange'] ?? true;
          break;
        case 'towel_change_frequency':
          this.dailyTowelChange = this.previousValues['dailyTowelChange'] ?? true;
          break;
        case 'dnd_schedule':
        case 'dnd_cancel':
          this.dndEnabled = this.previousValues['dndEnabled'] ?? false;
          this.dndUntilTime = this.previousValues['dndUntilTime'] ?? '14:00';
          break;
        case 'cleaning_time':
          this.cleaningTime = this.previousValues['cleaningTime'] ?? '';
          break;
        case 'extra_pillows':
        case 'extra_pillows_cancel':
          this.extraPillows = this.previousValues['extraPillows'] ?? 0;
          break;
        case 'extra_blankets':
        case 'extra_blankets_cancel':
          this.extraBlankets = this.previousValues['extraBlankets'] ?? 0;
          break;
        case 'pillow_type':
          this.pillowType = this.previousValues['pillowType'] ?? '';
          break;
      }
    }

    this.pendingPreference.set(null);
  }

  // ============================================
  // Request Methods (show confirmation before saving)
  // ============================================

  requestAirconPreference(): void {
    this.previousValues['airconEnabled'] = !this.airconEnabled; // Store the opposite (what it was before toggle)
    const description = this.airconEnabled
      ? 'Air conditioning will be turned on after cleaning your room'
      : 'Air conditioning will be left off after cleaning';

    this.showConfirmation({
      type: 'aircon_after_cleaning',
      value: { enabled: this.airconEnabled },
      label: 'Air Conditioning After Cleaning',
      description,
      icon: 'bi-thermometer-half'
    });
  }

  requestLinenPreference(): void {
    this.previousValues['dailyLinenChange'] = !this.dailyLinenChange;
    const description = this.dailyLinenChange
      ? 'Your bed linens will be changed daily'
      : 'Your bed linens will only be changed on request';

    this.showConfirmation({
      type: 'linen_change_frequency',
      value: { daily: this.dailyLinenChange },
      label: 'Daily Linen Change',
      description,
      icon: 'bi-droplet'
    });
  }

  requestTowelPreference(): void {
    this.previousValues['dailyTowelChange'] = !this.dailyTowelChange;
    const description = this.dailyTowelChange
      ? 'Your towels will be changed daily'
      : 'Your towels will only be changed on request';

    this.showConfirmation({
      type: 'towel_change_frequency',
      value: { daily: this.dailyTowelChange },
      label: 'Daily Towel Change',
      description,
      icon: 'bi-droplet'
    });
  }

  requestDndPreference(): void {
    this.previousValues['dndEnabled'] = !this.dndEnabled;
    this.previousValues['dndUntilTime'] = this.dndUntilTime;

    if (this.dndEnabled) {
      this.showConfirmation({
        type: 'dnd_schedule',
        value: { until: this.dndUntilTime },
        label: 'Do Not Disturb',
        description: `Housekeeping will not enter your room until ${this.dndUntilTime}`,
        icon: 'bi-moon'
      });
    } else {
      // Cancelling DND
      this.showConfirmation({
        type: 'dnd_cancel',
        value: null,
        label: 'Disable Do Not Disturb',
        description: 'Housekeeping will resume normal service',
        icon: 'bi-moon'
      });
    }
  }

  requestDndTimePreference(): void {
    this.previousValues['dndUntilTime'] = this.dndUntilTime;

    this.showConfirmation({
      type: 'dnd_schedule',
      value: { until: this.dndUntilTime },
      label: 'Do Not Disturb',
      description: `Housekeeping will not enter your room until ${this.dndUntilTime}`,
      icon: 'bi-moon'
    });
  }

  requestCleaningTime(time: string): void {
    this.previousValues['cleaningTime'] = this.cleaningTime;
    this.cleaningTime = time; // Update UI immediately for visual feedback

    let description: string;
    switch (time) {
      case 'morning':
        description = 'Your room will be cleaned in the morning (8-11am)';
        break;
      case 'afternoon':
        description = 'Your room will be cleaned in the afternoon (2-5pm)';
        break;
      case 'skip':
        description = 'Housekeeping will skip cleaning your room today';
        break;
      default:
        description = `Cleaning scheduled for ${time}`;
    }

    this.showConfirmation({
      type: 'cleaning_time',
      value: { time },
      label: 'Preferred Cleaning Time',
      description,
      icon: 'bi-clock'
    });
  }

  requestIncrementPillows(): void {
    if (this.extraPillows >= 4) return;

    this.previousValues['extraPillows'] = this.extraPillows;
    const newCount = this.extraPillows + 1;
    this.extraPillows = newCount; // Update UI

    this.showConfirmation({
      type: 'extra_pillows',
      value: { count: newCount },
      label: 'Extra Pillows',
      description: `We will bring ${newCount} extra pillow${newCount > 1 ? 's' : ''} to your room`,
      icon: 'bi-house-heart'
    });
  }

  requestDecrementPillows(): void {
    if (this.extraPillows <= 0) return;

    this.previousValues['extraPillows'] = this.extraPillows;
    const newCount = this.extraPillows - 1;
    this.extraPillows = newCount; // Update UI

    if (newCount === 0) {
      this.showConfirmation({
        type: 'extra_pillows_cancel',
        value: null,
        label: 'Remove Extra Pillows',
        description: 'The extra pillows will be removed from your room',
        icon: 'bi-house-heart'
      });
    } else {
      this.showConfirmation({
        type: 'extra_pillows',
        value: { count: newCount },
        label: 'Extra Pillows',
        description: `We will adjust to ${newCount} extra pillow${newCount > 1 ? 's' : ''} in your room`,
        icon: 'bi-house-heart'
      });
    }
  }

  requestPillowType(type: string): void {
    this.previousValues['pillowType'] = this.pillowType;
    this.pillowType = type; // Update UI

    const typeLabels: Record<string, string> = {
      'soft': 'soft',
      'medium': 'medium firmness',
      'firm': 'firm',
      'hypoallergenic': 'hypoallergenic'
    };

    this.showConfirmation({
      type: 'pillow_type',
      value: { type },
      label: 'Pillow Preference',
      description: `We will provide ${typeLabels[type] || type} pillows for your room`,
      icon: 'bi-house-heart'
    });
  }

  // ============================================
  // Extra Blankets Methods
  // ============================================

  requestIncrementBlankets(): void {
    if (this.extraBlankets >= 3) return;

    this.previousValues['extraBlankets'] = this.extraBlankets;
    const newCount = this.extraBlankets + 1;
    this.extraBlankets = newCount; // Update UI

    this.showConfirmation({
      type: 'extra_blankets',
      value: { count: newCount },
      label: 'Extra Blankets',
      description: `We will bring ${newCount} extra blanket${newCount > 1 ? 's' : ''} to your room`,
      icon: 'bi-cloud'
    });
  }

  requestDecrementBlankets(): void {
    if (this.extraBlankets <= 0) return;

    this.previousValues['extraBlankets'] = this.extraBlankets;
    const newCount = this.extraBlankets - 1;
    this.extraBlankets = newCount; // Update UI

    if (newCount === 0) {
      this.showConfirmation({
        type: 'extra_blankets_cancel',
        value: null,
        label: 'Remove Extra Blankets',
        description: 'The extra blankets will be removed from your room',
        icon: 'bi-cloud'
      });
    } else {
      this.showConfirmation({
        type: 'extra_blankets',
        value: { count: newCount },
        label: 'Extra Blankets',
        description: `We will adjust to ${newCount} extra blanket${newCount > 1 ? 's' : ''} in your room`,
        icon: 'bi-cloud'
      });
    }
  }

  // ============================================
  // Request Items Methods (Database-driven)
  // ============================================

  loadRequestItems(): void {
    // Load items from Housekeeping department
    this.guestApi.getRequestItems(undefined, 'Housekeeping').subscribe({
      next: (response) => {
        this.availableItems.set(response.items);
      },
      error: (err) => {
        console.error('Failed to load request items:', err);
      }
    });
  }

  isItemRequested(itemId: number): boolean {
    return this.requestedItems().has(itemId);
  }

  requestItem(item: RequestItem): void {
    const roomNumber = this.roomContext.getRoomNumber();

    this.requestingItem.set(item.id);

    this.guestApi.submitItemRequest({
      requestItemId: item.id,
      roomNumber: roomNumber || undefined,
      quantity: 1
    }).subscribe({
      next: (response) => {
        this.requestingItem.set(null);
        if (response.success) {
          // Add to requested items
          const current = new Set(this.requestedItems());
          current.add(item.id);
          this.requestedItems.set(current);

          // Show success toast
          this.showSuccessToast.set(true);
          setTimeout(() => this.showSuccessToast.set(false), 3000);
        } else {
          alert(response.message || 'Failed to submit request');
        }
      },
      error: (err) => {
        console.error('Failed to request item:', err);
        this.requestingItem.set(null);
        alert('Failed to submit request. Please try again.');
      }
    });
  }
}
