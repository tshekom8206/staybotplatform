import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { CurrencyService, ExchangeRates, SUPPORTED_CURRENCIES } from '../../../core/services/currency.service';

@Component({
  selector: 'app-currency-converter',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  template: `
    <div class="currency-wrapper">
      <!-- Header Button -->
      <button class="btn-currency" (click)="openSheet()">
        <i class="bi bi-currency-exchange"></i>
        <span>ZAR</span>
      </button>
    </div>

    <!-- Bottom Sheet Overlay -->
    <div class="sheet-overlay" [class.open]="sheetOpen" (click)="closeSheet()" *ngIf="sheetOpen">
      <div class="sheet-container" (click)="$event.stopPropagation()">
        <div class="sheet-handle"></div>

        <div class="sheet-header">
          <h3>{{ 'currency.title' | translate }}</h3>
          <button class="btn-close-sheet" (click)="closeSheet()">
            <i class="bi bi-x-lg"></i>
          </button>
        </div>

        <div class="sheet-content">
          <div class="input-group">
            <label>{{ 'currency.amount' | translate }}</label>
            <div class="amount-input">
              <input
                type="number"
                [(ngModel)]="amount"
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

          <div class="loading" *ngIf="isLoading && !rates">
            <i class="bi bi-arrow-repeat spin"></i>
            <span>{{ 'common.loading' | translate }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: contents;
    }

    .currency-wrapper {
      display: inline-block;
    }

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

    .sheet-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
      z-index: 2000;
      display: flex;
      align-items: flex-end;
      justify-content: center;
    }

    .sheet-container {
      background: white;
      border-radius: 20px 20px 0 0;
      width: 100%;
      max-width: 480px;
      max-height: 85vh;
      overflow-y: auto;
    }

    .sheet-handle {
      width: 40px;
      height: 4px;
      background: #ddd;
      border-radius: 2px;
      margin: 12px auto;
    }

    .sheet-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0 1.25rem 1rem;
      border-bottom: 1px solid #f0f0f0;
    }

    .sheet-header h3 {
      font-size: 1.1rem;
      font-weight: 600;
      margin: 0;
    }

    .btn-close-sheet {
      background: none;
      border: none;
      padding: 0.5rem;
      cursor: pointer;
      color: #666;
      font-size: 1.1rem;
    }

    .sheet-content {
      padding: 1.25rem;
    }

    .input-group {
      margin-bottom: 1.25rem;
    }

    .input-group label {
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
export class CurrencyConverterComponent implements OnInit {
  private currencyService = inject(CurrencyService);

  sheetOpen = false;
  isLoading = false;
  rates: ExchangeRates | null = null;
  amount = 100;
  displayCurrencies = SUPPORTED_CURRENCIES.slice(0, 6);

  ngOnInit(): void {
    this.loadRates();
  }

  openSheet(): void {
    this.sheetOpen = true;
    document.body.style.overflow = 'hidden';
  }

  closeSheet(): void {
    this.sheetOpen = false;
    document.body.style.overflow = '';
  }

  private loadRates(): void {
    this.isLoading = true;
    this.currencyService.getRates('ZAR').subscribe(data => {
      this.rates = data;
      this.isLoading = false;
    });
  }

  formatConversion(currencyCode: string): string {
    if (!this.rates || !this.rates.rates[currencyCode]) return '--';
    const converted = this.currencyService.convert(this.amount, this.rates, currencyCode);
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
