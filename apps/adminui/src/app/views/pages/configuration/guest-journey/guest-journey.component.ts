import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { NgbTooltipModule, NgbAlertModule, NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  GuestJourneyService,
  GuestJourneySettings,
  TemplatePlaceholder,
  ScheduledMessage
} from '../../../../core/services/guest-journey.service';

interface MessageTypeConfig {
  key: string;
  name: string;
  description: string;
  icon: string;
  enabledKey: keyof GuestJourneySettings;
  templateKey: keyof GuestJourneySettings;
  timeKey?: keyof GuestJourneySettings;
  hoursKey?: keyof GuestJourneySettings;
  daysKey?: keyof GuestJourneySettings;
  timeLabel?: string;
}

@Component({
  selector: 'app-guest-journey',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbNavModule,
    FeatherIconDirective
  ],
  template: `
    <div class="row">
      <div class="col-12">
        <div class="card">
          <div class="card-header d-flex justify-content-between align-items-center">
            <div>
              <h4 class="card-title mb-1">Guest Journey Automation</h4>
              <p class="text-muted mb-0">Configure automated WhatsApp messages throughout the guest stay</p>
            </div>
            <div class="d-flex gap-2">
              <button class="btn btn-outline-primary" (click)="loadSettings()" [disabled]="loading">
                <i class="feather icon-refresh-cw me-1"></i> Refresh
              </button>
              <button class="btn btn-primary" (click)="saveSettings()" [disabled]="loading || !hasChanges">
                <i class="feather icon-save me-1"></i> Save Changes
              </button>
            </div>
          </div>

          <div class="card-body">
            @if (error) {
              <ngb-alert type="danger" [dismissible]="true" (closed)="error = null">
                {{ error }}
              </ngb-alert>
            }

            @if (success) {
              <ngb-alert type="success" [dismissible]="true" (closed)="success = null">
                {{ success }}
              </ngb-alert>
            }

            @if (loading) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">Loading...</span>
                </div>
                <p class="mt-2 text-muted">Loading settings...</p>
              </div>
            } @else {
              <!-- Navigation Tabs -->
              <ul ngbNav #nav="ngbNav" [(activeId)]="activeTab" class="nav-tabs mb-4">
                <li [ngbNavItem]="'settings'">
                  <button ngbNavLink>
                    <i class="feather icon-settings me-2"></i>Message Settings
                  </button>
                  <ng-template ngbNavContent>
                    <div class="message-types-grid">
                      @for (config of messageTypeConfigs; track config.key) {
                        <div class="message-type-card" [class.enabled]="form.get(config.enabledKey)?.value">
                          <div class="card-header-section">
                            <div class="d-flex align-items-center gap-3">
                              <div class="type-icon" [class.enabled]="form.get(config.enabledKey)?.value">
                                <i class="feather" [attr.data-feather]="config.icon"></i>
                              </div>
                              <div>
                                <h5 class="mb-0">{{ config.name }}</h5>
                                <small class="text-muted">{{ config.description }}</small>
                              </div>
                            </div>
                            <div class="form-check form-switch">
                              <input
                                type="checkbox"
                                class="form-check-input"
                                [formControl]="$any(form.get(config.enabledKey))"
                                [id]="config.key + '_enabled'">
                            </div>
                          </div>

                          @if (form.get(config.enabledKey)?.value) {
                            <div class="card-body-section">
                              <!-- Timing Configuration -->
                              <div class="timing-row">
                                @if (config.daysKey) {
                                  <div class="form-group">
                                    <label class="form-label">Days Before</label>
                                    <input
                                      type="number"
                                      class="form-control"
                                      [formControl]="$any(form.get(config.daysKey))"
                                      min="1"
                                      max="14">
                                  </div>
                                }
                                @if (config.hoursKey) {
                                  <div class="form-group">
                                    <label class="form-label">Hours After Check-in</label>
                                    <input
                                      type="number"
                                      class="form-control"
                                      [formControl]="$any(form.get(config.hoursKey))"
                                      min="1"
                                      max="24">
                                  </div>
                                }
                                @if (config.timeKey) {
                                  <div class="form-group">
                                    <label class="form-label">{{ config.timeLabel || 'Send Time' }}</label>
                                    <input
                                      type="time"
                                      class="form-control"
                                      [formControl]="$any(form.get(config.timeKey))">
                                  </div>
                                }
                              </div>

                              <!-- Template Editor -->
                              <div class="template-section">
                                <div class="d-flex justify-content-between align-items-center mb-2">
                                  <label class="form-label mb-0">Message Template</label>
                                  <button
                                    class="btn btn-sm btn-outline-secondary"
                                    (click)="previewTemplate(config.templateKey)">
                                    <i class="feather icon-eye me-1"></i> Preview
                                  </button>
                                </div>
                                <textarea
                                  class="form-control template-textarea"
                                  [formControl]="$any(form.get(config.templateKey))"
                                  rows="6"
                                  placeholder="Enter your message template..."></textarea>
                                <div class="placeholders-hint">
                                  <small class="text-muted">Available placeholders: </small>
                                  @for (p of placeholders; track p.name) {
                                    <button
                                      type="button"
                                      class="btn btn-sm btn-outline-secondary placeholder-btn"
                                      [ngbTooltip]="p.description"
                                      (click)="insertPlaceholder(config.templateKey, p.name)">
                                      {{ p.name }}
                                    </button>
                                  }
                                </div>
                              </div>
                            </div>
                          }
                        </div>
                      }
                    </div>
                  </ng-template>
                </li>

                <li [ngbNavItem]="'scheduled'">
                  <button ngbNavLink>
                    <i class="feather icon-clock me-2"></i>Scheduled Messages
                  </button>
                  <ng-template ngbNavContent>
                    <div class="scheduled-messages-section">
                      <div class="d-flex justify-content-between align-items-center mb-3">
                        <div class="d-flex gap-2">
                          <select class="form-select" [(ngModel)]="statusFilter" (change)="loadScheduledMessages()">
                            <option value="">All Statuses</option>
                            <option value="Pending">Pending</option>
                            <option value="Sent">Sent</option>
                            <option value="Failed">Failed</option>
                            <option value="Cancelled">Cancelled</option>
                          </select>
                          <select class="form-select" [(ngModel)]="typeFilter" (change)="loadScheduledMessages()">
                            <option value="">All Types</option>
                            <option value="PreArrival">Pre-Arrival</option>
                            <option value="CheckinDay">Check-in Day</option>
                            <option value="WelcomeSettled">Welcome Settled</option>
                            <option value="MidStay">Mid-Stay</option>
                            <option value="PreCheckout">Pre-Checkout</option>
                            <option value="PostStay">Post-Stay</option>
                          </select>
                        </div>
                        <button class="btn btn-outline-primary btn-sm" (click)="loadScheduledMessages()">
                          <i class="feather icon-refresh-cw me-1"></i> Refresh
                        </button>
                      </div>

                      @if (loadingMessages) {
                        <div class="text-center py-4">
                          <div class="spinner-border spinner-border-sm text-primary"></div>
                        </div>
                      } @else if (scheduledMessages.length === 0) {
                        <div class="text-center py-4 text-muted">
                          <i class="feather icon-inbox" style="font-size: 2rem;"></i>
                          <p class="mt-2">No scheduled messages found</p>
                        </div>
                      } @else {
                        <div class="table-responsive">
                          <table class="table table-hover">
                            <thead>
                              <tr>
                                <th>Guest</th>
                                <th>Type</th>
                                <th>Scheduled For</th>
                                <th>Status</th>
                                <th>Actions</th>
                              </tr>
                            </thead>
                            <tbody>
                              @for (msg of scheduledMessages; track msg.id) {
                                <tr>
                                  <td>
                                    <div class="d-flex flex-column">
                                      <span class="fw-medium">{{ msg.guestName }}</span>
                                      <small class="text-muted">{{ msg.roomNumber || 'No room' }}</small>
                                    </div>
                                  </td>
                                  <td>
                                    <span class="badge bg-info">
                                      {{ guestJourneyService.getMessageTypeDisplayName(msg.messageType) }}
                                    </span>
                                  </td>
                                  <td>{{ msg.scheduledFor | date:'short' }}</td>
                                  <td>
                                    <span class="badge" [ngClass]="guestJourneyService.getStatusBadgeClass(msg.status)">
                                      {{ msg.status }}
                                    </span>
                                  </td>
                                  <td>
                                    <button
                                      class="btn btn-sm btn-outline-secondary"
                                      [ngbTooltip]="msg.content"
                                      placement="left">
                                      <i class="feather icon-eye"></i>
                                    </button>
                                  </td>
                                </tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      }
                    </div>
                  </ng-template>
                </li>
              </ul>

              <div [ngbNavOutlet]="nav"></div>
            }
          </div>
        </div>
      </div>
    </div>

    <!-- Preview Modal -->
    @if (showPreviewModal) {
      <div class="modal-backdrop fade show"></div>
      <div class="modal fade show d-block" tabindex="-1">
        <div class="modal-dialog modal-dialog-centered">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title">Message Preview</h5>
              <button type="button" class="btn-close" (click)="closePreview()"></button>
            </div>
            <div class="modal-body">
              <div class="preview-message">
                <div class="preview-bubble">
                  {{ previewContent }}
                </div>
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" (click)="closePreview()">Close</button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .message-types-grid {
      display: grid;
      gap: 1.5rem;
    }

    .message-type-card {
      border: 1px solid #e9ecef;
      border-radius: 8px;
      overflow: hidden;
      transition: all 0.2s ease;
    }

    .message-type-card.enabled {
      border-color: #0d6efd;
      box-shadow: 0 0 0 1px rgba(13, 110, 253, 0.1);
    }

    .card-header-section {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 1.25rem;
      background: #f8f9fa;
      border-bottom: 1px solid #e9ecef;
    }

    .message-type-card.enabled .card-header-section {
      background: linear-gradient(135deg, rgba(13, 110, 253, 0.05) 0%, rgba(13, 110, 253, 0.02) 100%);
    }

    .type-icon {
      width: 40px;
      height: 40px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #e9ecef;
      border-radius: 8px;
      color: #6c757d;
    }

    .type-icon.enabled {
      background: #0d6efd;
      color: white;
    }

    .card-body-section {
      padding: 1.25rem;
    }

    .timing-row {
      display: flex;
      gap: 1rem;
      margin-bottom: 1rem;
    }

    .timing-row .form-group {
      flex: 1;
    }

    .template-section {
      margin-top: 1rem;
    }

    .template-textarea {
      font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .placeholders-hint {
      margin-top: 0.5rem;
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem;
      align-items: center;
    }

    .placeholder-btn {
      padding: 0.125rem 0.5rem;
      font-size: 0.75rem;
      font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
    }

    .form-switch .form-check-input {
      width: 3em;
      height: 1.5em;
    }

    .scheduled-messages-section {
      min-height: 300px;
    }

    .preview-message {
      background: #f0f2f5;
      padding: 1rem;
      border-radius: 8px;
    }

    .preview-bubble {
      background: #dcf8c6;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      white-space: pre-wrap;
      font-size: 0.9rem;
      line-height: 1.5;
      max-width: 80%;
    }

    .modal.show {
      display: block;
    }
  `]
})
export class GuestJourneyComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  guestJourneyService = inject(GuestJourneyService);

  form!: FormGroup;
  originalSettings: GuestJourneySettings | null = null;

  loading = false;
  loadingMessages = false;
  error: string | null = null;
  success: string | null = null;

  activeTab = 'settings';
  placeholders: TemplatePlaceholder[] = [];
  scheduledMessages: ScheduledMessage[] = [];

  statusFilter = '';
  typeFilter = '';

  showPreviewModal = false;
  previewContent = '';

  messageTypeConfigs: MessageTypeConfig[] = [
    {
      key: 'preArrival',
      name: 'Pre-Arrival',
      description: 'Sent days before check-in to help guests prepare',
      icon: 'calendar',
      enabledKey: 'preArrivalEnabled',
      templateKey: 'preArrivalTemplate',
      timeKey: 'preArrivalTime',
      daysKey: 'preArrivalDaysBefore',
      timeLabel: 'Send Time'
    },
    {
      key: 'checkinDay',
      name: 'Check-in Day',
      description: 'Morning reminder on the day of arrival',
      icon: 'log-in',
      enabledKey: 'checkinDayEnabled',
      templateKey: 'checkinDayTemplate',
      timeKey: 'checkinDayTime',
      timeLabel: 'Send Time'
    },
    {
      key: 'welcomeSettled',
      name: 'Welcome Settled',
      description: 'Sent after check-in to check on the guest',
      icon: 'home',
      enabledKey: 'welcomeSettledEnabled',
      templateKey: 'welcomeSettledTemplate',
      hoursKey: 'welcomeSettledHoursAfter'
    },
    {
      key: 'midStay',
      name: 'Mid-Stay Check',
      description: 'Satisfaction check during longer stays',
      icon: 'sun',
      enabledKey: 'midStayEnabled',
      templateKey: 'midStayTemplate',
      timeKey: 'midStayTime',
      timeLabel: 'Send Time'
    },
    {
      key: 'preCheckout',
      name: 'Pre-Checkout',
      description: 'Day before checkout with departure info',
      icon: 'log-out',
      enabledKey: 'preCheckoutEnabled',
      templateKey: 'preCheckoutTemplate',
      timeKey: 'preCheckoutTime',
      timeLabel: 'Send Time'
    },
    {
      key: 'postStay',
      name: 'Post-Stay',
      description: 'Thank you and feedback request after checkout',
      icon: 'heart',
      enabledKey: 'postStayEnabled',
      templateKey: 'postStayTemplate',
      timeKey: 'postStayTime',
      timeLabel: 'Send Time'
    }
  ];

  get hasChanges(): boolean {
    if (!this.originalSettings) return false;
    return JSON.stringify(this.form.value) !== JSON.stringify(this.originalSettings);
  }

  ngOnInit(): void {
    this.initForm();
    this.loadSettings();
    this.loadPlaceholders();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      // Pre-Arrival
      preArrivalEnabled: [false],
      preArrivalDaysBefore: [3],
      preArrivalTime: ['10:00'],
      preArrivalTemplate: [''],

      // Check-in Day
      checkinDayEnabled: [true],
      checkinDayTime: ['09:00'],
      checkinDayTemplate: [''],

      // Welcome Settled
      welcomeSettledEnabled: [true],
      welcomeSettledHoursAfter: [3],
      welcomeSettledTemplate: [''],

      // Mid-Stay
      midStayEnabled: [true],
      midStayTime: ['10:00'],
      midStayTemplate: [''],

      // Pre-Checkout
      preCheckoutEnabled: [true],
      preCheckoutTime: ['18:00'],
      preCheckoutTemplate: [''],

      // Post-Stay
      postStayEnabled: [true],
      postStayTime: ['10:00'],
      postStayTemplate: [''],

      // Media & Other
      welcomeImageUrl: [''],
      includePhotoInWelcome: [true],
      timezone: ['Africa/Johannesburg']
    });
  }

  loadSettings(): void {
    this.loading = true;
    this.error = null;

    this.guestJourneyService.getSettings()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (settings) => {
          this.form.patchValue(settings);
          this.originalSettings = { ...settings };
          this.loading = false;
        },
        error: (err) => {
          console.error('Error loading settings:', err);
          this.error = 'Failed to load settings. Please try again.';
          this.loading = false;
        }
      });
  }

  loadPlaceholders(): void {
    this.guestJourneyService.getPlaceholders()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (placeholders) => {
          this.placeholders = placeholders;
        }
      });
  }

  loadScheduledMessages(): void {
    this.loadingMessages = true;

    this.guestJourneyService.getScheduledMessages(1, 50, this.statusFilter, this.typeFilter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.scheduledMessages = response.messages;
          this.loadingMessages = false;
        },
        error: () => {
          this.loadingMessages = false;
        }
      });
  }

  saveSettings(): void {
    if (!this.hasChanges) return;

    this.loading = true;
    this.error = null;

    this.guestJourneyService.updateSettings(this.form.value)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.originalSettings = { ...this.form.value };
          this.success = 'Settings saved successfully!';
          this.loading = false;
          setTimeout(() => this.success = null, 3000);
        },
        error: (err) => {
          console.error('Error saving settings:', err);
          this.error = 'Failed to save settings. Please try again.';
          this.loading = false;
        }
      });
  }

  insertPlaceholder(templateKey: keyof GuestJourneySettings, placeholder: string): void {
    const control = this.form.get(templateKey as string);
    if (control) {
      const currentValue = control.value || '';
      control.setValue(currentValue + placeholder);
    }
  }

  previewTemplate(templateKey: keyof GuestJourneySettings): void {
    const template = this.form.get(templateKey as string)?.value;
    if (!template) {
      this.previewContent = '(Empty template)';
      this.showPreviewModal = true;
      return;
    }

    this.guestJourneyService.previewTemplate(template)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.previewContent = response.preview;
          this.showPreviewModal = true;
        },
        error: () => {
          this.previewContent = template;
          this.showPreviewModal = true;
        }
      });
  }

  closePreview(): void {
    this.showPreviewModal = false;
    this.previewContent = '';
  }
}
