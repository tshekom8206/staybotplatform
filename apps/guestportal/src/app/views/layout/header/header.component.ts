import { Component, inject, OnInit, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { NgbDropdownModule, NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { TenantService } from '../../../core/services/tenant.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { CurrencyService, ExchangeRates, SUPPORTED_CURRENCIES } from '../../../core/services/currency.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule, NgbDropdownModule, FormsModule],
  template: `
    <header class="header">
      <div class="container">
        <div class="header-inner">
          <!-- Logo -->
          <a routerLink="/" class="header-logo">
            @if (tenant?.logoUrl) {
              <img [src]="tenant?.logoUrl" [alt]="tenant?.name" class="logo-img">
            } @else {
              <span class="logo-text">{{ tenant?.name || 'Guest Portal' }}</span>
            }
          </a>

          <!-- Header Controls -->
          <div class="header-controls">
            <!-- Currency Button -->
            <button class="btn-currency" (click)="openCurrencyModal(currencyModal)">
              <i class="bi bi-currency-exchange"></i>
              <span>ZAR</span>
            </button>

            <!-- Language Selector -->
            <div ngbDropdown class="language-dropdown">
              <button class="btn-language" ngbDropdownToggle>
                <span class="flag">{{ currentLanguage.flag }}</span>
                <span class="code">{{ currentLanguage.code.toUpperCase() }}</span>
                <i class="bi bi-chevron-down"></i>
              </button>
              <div ngbDropdownMenu class="dropdown-menu-end language-menu">
                @for (lang of languages; track lang.code) {
                  <button ngbDropdownItem (click)="setLanguage(lang.code)"
                          [class.active]="lang.code === currentLanguage.code">
                    <span class="flag">{{ lang.flag }}</span>
                    <span>{{ lang.nativeName }}</span>
                  </button>
                }
              </div>
            </div>
          </div>
        </div>
      </div>
    </header>

    <!-- Currency Modal Template -->
    <ng-template #currencyModal let-modal>
      <div class="modal-header">
        <h5 class="modal-title">{{ 'currency.title' | translate }}</h5>
        <button type="button" class="btn-close" aria-label="Close" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body">
        <div class="input-group-currency">
          <label>{{ 'currency.amount' | translate }}</label>
          <div class="amount-input">
            <input
              type="number"
              [(ngModel)]="currencyAmount"
              placeholder="100"
              min="0"
            />
            <span class="currency-code">ZAR</span>
          </div>
        </div>

        <div class="conversions" *ngIf="rates">
          <div class="conversion-row" *ngFor="let currency of displayCurrencies">
            <span class="flag">{{ getCurrencyFlag(currency.code) }}</span>
            <span class="currency-name">{{ currency.code }}</span>
            <span class="converted-value">{{ formatConversion(currency.code) }}</span>
          </div>
          <div class="last-updated">
            {{ 'currency.lastUpdated' | translate }}: {{ getTimeSinceUpdate() }}
          </div>
        </div>

        <div class="loading" *ngIf="currencyLoading && !rates">
          <i class="bi bi-arrow-repeat spin"></i>
          <span>{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    </ng-template>
  `,
  styles: [`
    .header {
      background: rgba(255, 255, 255, 0.85);
      backdrop-filter: blur(20px);
      -webkit-backdrop-filter: blur(20px);
      position: sticky;
      top: 0;
      z-index: 1000;
      border-bottom: 1px solid rgba(255, 255, 255, 0.3);
    }

    .header-inner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 0;
    }

    .header-logo {
      text-decoration: none;
      color: inherit;
    }

    .logo-img {
      max-height: 36px;
      width: auto;
    }

    .logo-text {
      font-size: 1.1rem;
      font-weight: 600;
      color: #1a1a1a;
      letter-spacing: -0.02em;
    }

    /* Header Controls */
    .header-controls {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    /* Language Button */
    .btn-language {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.5rem 0.75rem;
      background: rgba(255, 255, 255, 0.9);
      border: 1px solid rgba(0, 0, 0, 0.1);
      border-radius: 50px;
      font-size: 0.8rem;
      font-weight: 500;
      color: #1a1a1a;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .btn-language:hover {
      background: white;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }

    .btn-language .flag {
      font-size: 1rem;
    }

    .btn-language .code {
      font-size: 0.75rem;
      font-weight: 600;
    }

    .btn-language i {
      font-size: 0.65rem;
      opacity: 0.6;
    }

    /* Dropdown Menu */
    .language-menu {
      border: none;
      border-radius: 12px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
      padding: 0.5rem;
      margin-top: 0.5rem;
    }

    .language-menu button {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 0.75rem;
      border-radius: 8px;
      font-size: 0.85rem;
    }

    .language-menu button:hover {
      background: #f5f5f5;
    }

    .language-menu button.active {
      background: #1a1a1a;
      color: white;
    }

    .language-menu .flag {
      font-size: 1.1rem;
    }

    /* Currency Button */
    .btn-currency {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.5rem 0.75rem;
      background: rgba(255, 255, 255, 0.9);
      border: 1px solid rgba(0, 0, 0, 0.1);
      border-radius: 50px;
      font-size: 0.8rem;
      font-weight: 500;
      color: #1a1a1a;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .btn-currency:hover {
      background: white;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }

    .btn-currency i {
      font-size: 1rem;
    }

    /* Currency Modal Styles */
    .input-group-currency {
      margin-bottom: 1.25rem;
    }

    .input-group-currency label {
      display: block;
      font-size: 0.85rem;
      font-weight: 500;
      color: #666;
      margin-bottom: 0.5rem;
    }

    .amount-input {
      display: flex;
      align-items: center;
      background: #f8f9fa;
      border: 2px solid #e9ecef;
      border-radius: 12px;
      padding: 0.75rem 1rem;
    }

    .amount-input input {
      flex: 1;
      border: none;
      background: none;
      font-size: 1.5rem;
      font-weight: 600;
      outline: none;
      min-width: 0;
    }

    .amount-input .currency-code {
      font-size: 1rem;
      font-weight: 600;
      color: #666;
    }

    .conversions {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .conversion-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem;
      background: #f8f9fa;
      border-radius: 10px;
    }

    .conversion-row .flag {
      font-size: 1.25rem;
    }

    .conversion-row .currency-name {
      font-size: 0.85rem;
      font-weight: 600;
      color: #666;
      min-width: 40px;
    }

    .conversion-row .converted-value {
      flex: 1;
      text-align: right;
      font-size: 1rem;
      font-weight: 600;
    }

    .last-updated {
      text-align: center;
      font-size: 0.75rem;
      color: #999;
      margin-top: 1rem;
      padding-top: 1rem;
      border-top: 1px solid #f0f0f0;
    }

    .loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 2rem;
      color: #666;
    }

    .spin {
      animation: spin 1s linear infinite;
    }

    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }

    input[type="number"]::-webkit-outer-spin-button,
    input[type="number"]::-webkit-inner-spin-button {
      -webkit-appearance: none;
      margin: 0;
    }
  `]
})
export class HeaderComponent implements OnInit {
  private tenantService = inject(TenantService);
  private languageService = inject(LanguageService);
  private currencyService = inject(CurrencyService);
  private modalService = inject(NgbModal);

  tenant = this.tenantService.getCurrentTenant();
  languages = this.languageService.supportedLanguages;
  currentLanguage = this.languageService.getCurrentLanguage();

  // Currency properties
  currencyAmount = 100;
  currencyLoading = false;
  rates: ExchangeRates | null = null;
  displayCurrencies = SUPPORTED_CURRENCIES.slice(0, 6);

  constructor() {
    this.tenantService.tenant$.subscribe(tenant => {
      this.tenant = tenant;
    });

    this.languageService.currentLanguage$.subscribe(lang => {
      this.currentLanguage = lang;
    });
  }

  ngOnInit(): void {
    this.loadRates();
  }

  setLanguage(code: string): void {
    this.languageService.setLanguage(code);
  }

  // Currency methods
  openCurrencyModal(content: TemplateRef<any>): void {
    this.modalService.open(content, { centered: true, size: 'sm' });
  }

  private loadRates(): void {
    this.currencyLoading = true;
    this.currencyService.getRates('ZAR').subscribe(data => {
      this.rates = data;
      this.currencyLoading = false;
    });
  }

  formatConversion(currencyCode: string): string {
    if (!this.rates || !this.rates.rates[currencyCode]) return '--';
    const converted = this.currencyService.convert(this.currencyAmount, this.rates, currencyCode);
    return this.currencyService.formatCurrency(converted, currencyCode);
  }

  getCurrencyFlag(currencyCode: string): string {
    return this.currencyService.getCurrencyFlag(currencyCode);
  }

  getTimeSinceUpdate(): string {
    if (!this.rates) return '';
    return this.currencyService.getTimeSinceUpdate(this.rates.lastUpdated);
  }
}
