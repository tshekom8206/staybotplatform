import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, FoundItem, LostItemRequest } from '../../../core/services/guest-api.service';
import { RoomContextService } from '../../../core/services/room-context.service';

@Component({
  selector: 'app-lost-found',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header with Glassmorphism -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'lostFound.title' | translate }}</h1>
        </div>

        <!-- Tab Navigation -->
        <div class="tab-nav mb-4">
          <button
            class="tab-btn"
            [class.active]="activeTab() === 'found'"
            (click)="activeTab.set('found')">
            <i class="bi bi-search"></i> {{ 'lostFound.foundItems' | translate }}
          </button>
          <button
            class="tab-btn"
            [class.active]="activeTab() === 'report'"
            (click)="activeTab.set('report')">
            <i class="bi bi-plus-circle"></i> {{ 'lostFound.reportLost' | translate }}
          </button>
        </div>

        @if (activeTab() === 'found') {
          <!-- Found Items List -->
          @if (loadingItems()) {
            <div class="loading-spinner">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (foundItems().length === 0) {
            <div class="empty-state">
              <i class="bi bi-box-seam"></i>
              <p>{{ 'lostFound.noFoundItems' | translate }}</p>
            </div>
          } @else {
            <p class="text-muted mb-3">{{ 'lostFound.foundItemsDescription' | translate }}</p>
            <div class="found-items">
              @for (item of foundItems(); track item.id) {
                <div class="found-item-card">
                  <div class="item-category">
                    <i class="bi" [class]="getCategoryIcon(item.category)"></i>
                  </div>
                  <div class="item-info">
                    <h4>{{ item.itemName }}</h4>
                    @if (item.description) {
                      <p class="description">{{ item.description }}</p>
                    }
                    <div class="item-meta">
                      @if (item.color) {
                        <span><i class="bi bi-palette"></i> {{ item.color }}</span>
                      }
                      @if (item.brand) {
                        <span><i class="bi bi-tag"></i> {{ item.brand }}</span>
                      }
                      @if (item.locationFound) {
                        <span><i class="bi bi-geo-alt"></i> {{ item.locationFound }}</span>
                      }
                    </div>
                    <span class="found-date">{{ 'lostFound.foundOn' | translate }}: {{ item.foundDate }}</span>
                  </div>
                </div>
              }
            </div>
            <p class="contact-note mt-3">
              <i class="bi bi-info-circle"></i> {{ 'lostFound.contactNote' | translate }}
            </p>
          }
        } @else {
          <!-- Report Lost Item Form -->
          @if (submitted()) {
            <div class="success-message">
              <i class="bi bi-check-circle-fill"></i>
              <h3>{{ 'lostFound.reportSubmitted' | translate }}</h3>
              <p>{{ 'lostFound.reportSubmittedMessage' | translate }}</p>
              <button class="btn btn-primary" (click)="resetForm()">
                {{ 'lostFound.reportAnother' | translate }}
              </button>
            </div>
          } @else {
            <p class="text-muted mb-3">{{ 'lostFound.reportDescription' | translate }}</p>
            <form (ngSubmit)="submitReport()" class="lost-item-form">
              <!-- Item Name -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.itemName' | translate }} *</label>
                <input
                  type="text"
                  class="form-control"
                  [(ngModel)]="formData.itemName"
                  name="itemName"
                  required
                  placeholder="{{ 'lostFound.itemNamePlaceholder' | translate }}">
              </div>

              <!-- Category -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.category' | translate }}</label>
                <select class="form-select" [(ngModel)]="formData.category" name="category">
                  <option value="">{{ 'lostFound.selectCategory' | translate }}</option>
                  <option value="Electronics">{{ 'lostFound.categoryElectronics' | translate }}</option>
                  <option value="Jewelry">{{ 'lostFound.categoryJewelry' | translate }}</option>
                  <option value="Clothing">{{ 'lostFound.categoryClothing' | translate }}</option>
                  <option value="Documents">{{ 'lostFound.categoryDocuments' | translate }}</option>
                  <option value="Bags">{{ 'lostFound.categoryBags' | translate }}</option>
                  <option value="Keys">{{ 'lostFound.categoryKeys' | translate }}</option>
                  <option value="Other">{{ 'lostFound.categoryOther' | translate }}</option>
                </select>
              </div>

              <!-- Description -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.description' | translate }}</label>
                <textarea
                  class="form-control"
                  [(ngModel)]="formData.description"
                  name="description"
                  rows="3"
                  placeholder="{{ 'lostFound.descriptionPlaceholder' | translate }}"></textarea>
              </div>

              <!-- Color & Brand -->
              <div class="row">
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.color' | translate }}</label>
                  <input
                    type="text"
                    class="form-control"
                    [(ngModel)]="formData.color"
                    name="color"
                    placeholder="{{ 'lostFound.colorPlaceholder' | translate }}">
                </div>
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.brand' | translate }}</label>
                  <input
                    type="text"
                    class="form-control"
                    [(ngModel)]="formData.brand"
                    name="brand"
                    placeholder="{{ 'lostFound.brandPlaceholder' | translate }}">
                </div>
              </div>

              <!-- Location Lost -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.locationLost' | translate }}</label>
                <input
                  type="text"
                  class="form-control"
                  [(ngModel)]="formData.locationLost"
                  name="locationLost"
                  placeholder="{{ 'lostFound.locationPlaceholder' | translate }}">
              </div>

              <!-- Contact Info -->
              <div class="row">
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.yourName' | translate }}</label>
                  <input
                    type="text"
                    class="form-control"
                    [(ngModel)]="formData.guestName"
                    name="guestName"
                    placeholder="{{ 'lostFound.namePlaceholder' | translate }}">
                </div>
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.phone' | translate }}</label>
                  <input
                    type="tel"
                    class="form-control"
                    [(ngModel)]="formData.phone"
                    name="phone"
                    placeholder="{{ 'lostFound.phonePlaceholder' | translate }}">
                </div>
              </div>

              <!-- Room Number -->
              <div class="mb-4">
                <label class="form-label">{{ 'lostFound.roomNumber' | translate }}</label>
                <input
                  type="text"
                  class="form-control"
                  [(ngModel)]="formData.roomNumber"
                  name="roomNumber"
                  placeholder="{{ 'lostFound.roomPlaceholder' | translate }}">
              </div>

              <button
                type="submit"
                class="btn btn-primary btn-lg w-100"
                [disabled]="submitting() || !formData.itemName">
                @if (submitting()) {
                  <span class="spinner-border spinner-border-sm me-2"></span>
                }
                {{ 'lostFound.submitReport' | translate }}
              </button>
            </form>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1rem 0; }

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
    .back-link:hover { background: rgba(255, 255, 255, 0.15); color: white; }
    .back-link i { font-size: 1rem; }
    .page-title {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }

    .tab-nav {
      display: flex;
      gap: 0.5rem;
      border-bottom: 2px solid #e9ecef;
      padding-bottom: 0;
    }
    .tab-btn {
      flex: 1;
      padding: 0.75rem 1rem;
      border: none;
      background: transparent;
      color: #666;
      font-weight: 500;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      transition: all 0.2s;
    }
    .tab-btn:hover { color: var(--theme-primary, #1976d2); }
    .tab-btn.active {
      color: var(--theme-primary, #1976d2);
      border-bottom-color: var(--theme-primary, #1976d2);
    }
    .tab-btn i { margin-right: 0.5rem; }

    .loading-spinner {
      display: flex;
      justify-content: center;
      padding: 3rem;
    }
    .empty-state {
      text-align: center;
      padding: 3rem;
      background: #f8f9fa;
      border-radius: 16px;
      color: #666;
    }
    .empty-state i { font-size: 3rem; margin-bottom: 1rem; opacity: 0.5; display: block; }

    .found-items {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .found-item-card {
      display: flex;
      gap: 1rem;
      padding: 1rem;
      background: white;
      border-radius: 12px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }
    .item-category {
      width: 48px;
      height: 48px;
      background: #1a1a1a;
      color: white;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
      flex-shrink: 0;
    }
    .item-info { flex: 1; }
    .item-info h4 { margin: 0 0 0.25rem; font-size: 1rem; font-weight: 600; }
    .item-info .description {
      margin: 0 0 0.5rem;
      font-size: 0.85rem;
      color: #666;
    }
    .item-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      font-size: 0.8rem;
      color: #888;
    }
    .item-meta span { display: flex; align-items: center; gap: 0.25rem; }
    .found-date {
      display: block;
      margin-top: 0.5rem;
      font-size: 0.75rem;
      color: #27ae60;
    }
    .contact-note {
      font-size: 0.85rem;
      color: #666;
      background: #f8f9fa;
      padding: 1rem;
      border-radius: 8px;
    }
    .contact-note i { margin-right: 0.5rem; color: var(--theme-primary, #1976d2); }

    .success-message {
      text-align: center;
      padding: 3rem 1.5rem;
      background: #e8f5e9;
      border-radius: 16px;
    }
    .success-message i {
      font-size: 4rem;
      color: #27ae60;
      margin-bottom: 1rem;
    }
    .success-message h3 { margin-bottom: 0.5rem; color: #27ae60; }
    .success-message p { color: #666; margin-bottom: 1.5rem; }

    .lost-item-form {
      background: white;
      padding: 1.5rem;
      border-radius: 16px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }
  `]
})
export class LostFoundComponent implements OnInit {
  private apiService = inject(GuestApiService);
  private roomContext = inject(RoomContextService);

  activeTab = signal<'found' | 'report'>('found');
  foundItems = signal<FoundItem[]>([]);
  loadingItems = signal(true);
  submitting = signal(false);
  submitted = signal(false);

  formData: LostItemRequest = {
    itemName: '',
    category: '',
    description: '',
    color: '',
    brand: '',
    locationLost: '',
    guestName: '',
    phone: '',
    roomNumber: ''
  };

  ngOnInit(): void {
    this.loadFoundItems();
    // Pre-fill room number from context
    const room = this.roomContext.getRoomNumber();
    if (room) {
      this.formData.roomNumber = room;
    }
  }

  loadFoundItems(): void {
    this.loadingItems.set(true);
    this.apiService.getFoundItems().subscribe({
      next: (response) => {
        this.foundItems.set(response.items);
        this.loadingItems.set(false);
      },
      error: (error) => {
        console.error('Failed to load found items:', error);
        this.loadingItems.set(false);
      }
    });
  }

  getCategoryIcon(category: string): string {
    const icons: { [key: string]: string } = {
      'Electronics': 'bi-phone',
      'Jewelry': 'bi-gem',
      'Clothing': 'bi-bag',
      'Documents': 'bi-file-earmark-text',
      'Bags': 'bi-briefcase',
      'Keys': 'bi-key',
      'Other': 'bi-box'
    };
    return icons[category] || 'bi-box';
  }

  submitReport(): void {
    if (!this.formData.itemName) return;

    this.submitting.set(true);
    this.apiService.reportLostItem(this.formData).subscribe({
      next: (response) => {
        this.submitting.set(false);
        if (response.success) {
          this.submitted.set(true);
        }
      },
      error: (error) => {
        console.error('Failed to submit report:', error);
        this.submitting.set(false);
      }
    });
  }

  resetForm(): void {
    this.formData = {
      itemName: '',
      category: '',
      description: '',
      color: '',
      brand: '',
      locationLost: '',
      guestName: '',
      phone: '',
      roomNumber: this.roomContext.getRoomNumber() || ''
    };
    this.submitted.set(false);
  }
}
