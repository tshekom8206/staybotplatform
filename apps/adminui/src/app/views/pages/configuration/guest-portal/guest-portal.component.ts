import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { environment } from '../../../../../environments/environment';

interface GuestPortalSettings {
  logoUrl: string | null;
  backgroundImageUrl: string | null;
  whatsappNumber: string | null;
  guestPortalEnabled: boolean;
}

@Component({
  selector: 'app-guest-portal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FeatherIconDirective],
  template: `
    <div class="page-content">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center flex-wrap grid-margin">
        <div>
          <h4 class="mb-3 mb-md-0">Guest Portal Settings</h4>
          <p class="text-muted mb-0">Customize your guest-facing mobile portal</p>
        </div>
        <div class="d-flex align-items-center flex-wrap text-nowrap">
          <button type="button"
                  class="btn btn-outline-secondary btn-icon-text me-2"
                  (click)="openPreview()"
                  [disabled]="loading()">
            <i data-feather="external-link" featherIcon></i> Preview Portal
          </button>
          <button type="button"
                  class="btn btn-primary btn-icon-text"
                  (click)="saveSettings()"
                  [disabled]="loading() || saving()">
            @if (saving()) {
              <span class="spinner-border spinner-border-sm me-2"></span>
            } @else {
              <i data-feather="save" featherIcon class="me-1"></i>
            }
            {{ saving() ? 'Saving...' : 'Save Changes' }}
          </button>
        </div>
      </div>

      <!-- Messages -->
      @if (success()) {
        <div class="alert alert-success alert-dismissible fade show">
          <i data-feather="check-circle" featherIcon class="me-2"></i>
          {{ success() }}
          <button type="button" class="btn-close" (click)="success.set(null)"></button>
        </div>
      }
      @if (error()) {
        <div class="alert alert-danger alert-dismissible fade show">
          <i data-feather="alert-triangle" featherIcon class="me-2"></i>
          {{ error() }}
          <button type="button" class="btn-close" (click)="error.set(null)"></button>
        </div>
      }

      <!-- Loading -->
      @if (loading()) {
        <div class="card">
          <div class="card-body text-center py-5">
            <div class="spinner-border text-primary"></div>
            <p class="text-muted mt-2">Loading settings...</p>
          </div>
        </div>
      } @else {
        <div class="row">
          <!-- Branding Settings -->
          <div class="col-lg-6 mb-4">
            <div class="card">
              <div class="card-header">
                <h6 class="card-title mb-0">
                  <i data-feather="image" featherIcon class="me-2"></i>
                  Branding
                </h6>
              </div>
              <div class="card-body">
                <form [formGroup]="form">
                  <!-- Logo URL -->
                  <div class="mb-4">
                    <label class="form-label">Logo URL</label>
                    <input type="url"
                           class="form-control"
                           formControlName="logoUrl"
                           placeholder="https://example.com/logo.png">
                    <small class="text-muted">
                      Enter the URL of your hotel logo (recommended: 200x80px, PNG with transparency)
                    </small>
                    @if (form.get('logoUrl')?.value) {
                      <div class="mt-3 p-3 bg-light rounded text-center">
                        <p class="text-muted small mb-2">Logo Preview:</p>
                        <img [src]="form.get('logoUrl')?.value"
                             alt="Logo preview"
                             class="img-fluid"
                             style="max-height: 80px;"
                             (error)="onImageError($event)">
                      </div>
                    }
                  </div>

                  <!-- Background Image URL -->
                  <div class="mb-4">
                    <label class="form-label">Background Image URL</label>
                    <input type="url"
                           class="form-control"
                           formControlName="backgroundImageUrl"
                           placeholder="https://example.com/background.jpg">
                    <small class="text-muted">
                      Enter the URL of your background image (recommended: 1920x1080px, JPG)
                    </small>
                    @if (form.get('backgroundImageUrl')?.value) {
                      <div class="mt-3 rounded overflow-hidden position-relative" style="height: 150px;">
                        <img [src]="form.get('backgroundImageUrl')?.value"
                             alt="Background preview"
                             class="w-100 h-100"
                             style="object-fit: cover; filter: blur(4px);"
                             (error)="onImageError($event)">
                        <div class="position-absolute top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center"
                             style="background: rgba(0,0,0,0.3);">
                          <span class="text-white fw-bold">Background Preview</span>
                        </div>
                      </div>
                    }
                  </div>

                  <!-- WhatsApp Number -->
                  <div class="mb-4">
                    <label class="form-label">WhatsApp Contact Number</label>
                    <input type="tel"
                           class="form-control"
                           formControlName="whatsappNumber"
                           placeholder="+27123456789">
                    <small class="text-muted">
                      Phone number for the "Contact Us" button (include country code)
                    </small>
                  </div>
                </form>
              </div>
            </div>
          </div>

          <!-- Portal Settings & Preview -->
          <div class="col-lg-6 mb-4">
            <div class="card mb-4">
              <div class="card-header">
                <h6 class="card-title mb-0">
                  <i data-feather="settings" featherIcon class="me-2"></i>
                  Portal Settings
                </h6>
              </div>
              <div class="card-body">
                <form [formGroup]="form">
                  <!-- Enable/Disable -->
                  <div class="form-check form-switch mb-3">
                    <input class="form-check-input"
                           type="checkbox"
                           id="guestPortalEnabled"
                           formControlName="guestPortalEnabled">
                    <label class="form-check-label" for="guestPortalEnabled">
                      <strong>Guest Portal Enabled</strong>
                      <small class="text-muted d-block">
                        When disabled, guests will see a "Coming Soon" page
                      </small>
                    </label>
                  </div>
                </form>

                <!-- Portal URL -->
                <div class="mt-4 p-3 bg-light rounded">
                  <label class="form-label text-muted small mb-1">Your Guest Portal URL:</label>
                  <div class="d-flex align-items-center">
                    <code class="flex-grow-1 p-2 bg-white rounded border">
                      {{ portalUrl }}
                    </code>
                    <button class="btn btn-sm btn-outline-primary ms-2"
                            (click)="copyUrl()"
                            title="Copy URL">
                      <i data-feather="copy" featherIcon></i>
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <!-- Mobile Preview -->
            <div class="card">
              <div class="card-header">
                <h6 class="card-title mb-0">
                  <i data-feather="smartphone" featherIcon class="me-2"></i>
                  Mobile Preview
                </h6>
              </div>
              <div class="card-body text-center">
                <div class="mobile-preview mx-auto">
                  <div class="mobile-frame">
                    <div class="mobile-screen"
                         [style.background-image]="form.get('backgroundImageUrl')?.value ? 'url(' + form.get('backgroundImageUrl')?.value + ')' : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)'">
                      <div class="mobile-overlay"></div>
                      <div class="mobile-content">
                        @if (form.get('logoUrl')?.value) {
                          <img [src]="form.get('logoUrl')?.value"
                               alt="Logo"
                               class="mobile-logo"
                               (error)="onImageError($event)">
                        } @else {
                          <div class="mobile-logo-placeholder">
                            <i data-feather="image" featherIcon></i>
                          </div>
                        }
                        <div class="mobile-title">Welcome</div>
                        <div class="mobile-subtitle">How can we help?</div>
                        <div class="mobile-grid">
                          <div class="mobile-card"></div>
                          <div class="mobile-card"></div>
                          <div class="mobile-card"></div>
                          <div class="mobile-card"></div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .mobile-preview {
      max-width: 220px;
    }

    .mobile-frame {
      background: #1a1a1a;
      border-radius: 24px;
      padding: 8px;
      box-shadow: 0 10px 40px rgba(0,0,0,0.3);
    }

    .mobile-screen {
      background-size: cover;
      background-position: center;
      border-radius: 18px;
      height: 380px;
      position: relative;
      overflow: hidden;
    }

    .mobile-overlay {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0,0,0,0.3);
      backdrop-filter: blur(8px);
    }

    .mobile-content {
      position: relative;
      z-index: 1;
      padding: 20px 15px;
      color: white;
      text-align: center;
    }

    .mobile-logo {
      max-height: 40px;
      max-width: 120px;
      margin-bottom: 15px;
      object-fit: contain;
    }

    .mobile-logo-placeholder {
      width: 50px;
      height: 50px;
      background: rgba(255,255,255,0.2);
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto 15px;
    }

    .mobile-title {
      font-size: 18px;
      font-weight: 700;
      margin-bottom: 4px;
    }

    .mobile-subtitle {
      font-size: 12px;
      opacity: 0.8;
      margin-bottom: 20px;
    }

    .mobile-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 10px;
    }

    .mobile-card {
      background: rgba(255,255,255,0.9);
      border-radius: 12px;
      height: 70px;
    }
  `]
})
export class GuestPortalComponent implements OnInit {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);

  form: FormGroup;
  loading = signal(true);
  saving = signal(false);
  success = signal<string | null>(null);
  error = signal<string | null>(null);

  portalUrl = '';

  constructor() {
    this.form = this.fb.group({
      logoUrl: [''],
      backgroundImageUrl: [''],
      whatsappNumber: [''],
      guestPortalEnabled: [true]
    });
  }

  ngOnInit(): void {
    this.loadSettings();
  }

  loadSettings(): void {
    this.loading.set(true);
    this.http.get<GuestPortalSettings>(`${environment.apiUrl}/api/tenant/portal-settings`)
      .subscribe({
        next: (settings) => {
          this.form.patchValue({
            logoUrl: settings.logoUrl || '',
            backgroundImageUrl: settings.backgroundImageUrl || '',
            whatsappNumber: settings.whatsappNumber || '',
            guestPortalEnabled: settings.guestPortalEnabled
          });
          this.loading.set(false);

          // Build portal URL from tenant slug
          const tenantSlug = localStorage.getItem('tenantSlug') || 'demo';
          this.portalUrl = `https://${tenantSlug}.staybot.co.za`;
        },
        error: (err) => {
          console.error('Error loading settings:', err);
          this.error.set('Failed to load settings');
          this.loading.set(false);

          // Set default portal URL
          const tenantSlug = localStorage.getItem('tenantSlug') || 'demo';
          this.portalUrl = `https://${tenantSlug}.staybot.co.za`;
        }
      });
  }

  saveSettings(): void {
    this.saving.set(true);
    this.error.set(null);

    const settings: GuestPortalSettings = {
      logoUrl: this.form.get('logoUrl')?.value || null,
      backgroundImageUrl: this.form.get('backgroundImageUrl')?.value || null,
      whatsappNumber: this.form.get('whatsappNumber')?.value || null,
      guestPortalEnabled: this.form.get('guestPortalEnabled')?.value
    };

    this.http.put(`${environment.apiUrl}/api/tenant/portal-settings`, settings)
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Settings saved successfully!');
          setTimeout(() => this.success.set(null), 5000);
        },
        error: (err) => {
          console.error('Error saving settings:', err);
          this.saving.set(false);
          this.error.set('Failed to save settings. Please try again.');
        }
      });
  }

  openPreview(): void {
    window.open(this.portalUrl, '_blank');
  }

  copyUrl(): void {
    navigator.clipboard.writeText(this.portalUrl);
    this.success.set('URL copied to clipboard!');
    setTimeout(() => this.success.set(null), 3000);
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
  }
}
