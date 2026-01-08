import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError, tap } from 'rxjs/operators';
import { isPlatformBrowser } from '@angular/common';

export interface ExchangeRates {
  base: string;
  rates: { [currency: string]: number };
  lastUpdated: Date;
}

export interface CurrencyInfo {
  code: string;
  name: string;
  symbol: string;
  flag: string;
}

// Common currencies for travelers
export const SUPPORTED_CURRENCIES: CurrencyInfo[] = [
  { code: 'USD', name: 'US Dollar', symbol: '$', flag: 'US' },
  { code: 'EUR', name: 'Euro', symbol: '\u20AC', flag: 'EU' },
  { code: 'GBP', name: 'British Pound', symbol: '\u00A3', flag: 'GB' },
  { code: 'CNY', name: 'Chinese Yuan', symbol: '\u00A5', flag: 'CN' },
  { code: 'JPY', name: 'Japanese Yen', symbol: '\u00A5', flag: 'JP' },
  { code: 'AUD', name: 'Australian Dollar', symbol: 'A$', flag: 'AU' },
  { code: 'CAD', name: 'Canadian Dollar', symbol: 'C$', flag: 'CA' },
  { code: 'CHF', name: 'Swiss Franc', symbol: 'CHF', flag: 'CH' },
  { code: 'INR', name: 'Indian Rupee', symbol: '\u20B9', flag: 'IN' },
  { code: 'AED', name: 'UAE Dirham', symbol: 'AED', flag: 'AE' }
];

// Currency code to flag emoji mapping
const currencyFlags: Record<string, string> = {
  'USD': '\uD83C\uDDFA\uD83C\uDDF8',
  'EUR': '\uD83C\uDDEA\uD83C\uDDFA',
  'GBP': '\uD83C\uDDEC\uD83C\uDDE7',
  'CNY': '\uD83C\uDDE8\uD83C\uDDF3',
  'JPY': '\uD83C\uDDEF\uD83C\uDDF5',
  'AUD': '\uD83C\uDDE6\uD83C\uDDFA',
  'CAD': '\uD83C\uDDE8\uD83C\uDDE6',
  'CHF': '\uD83C\uDDE8\uD83C\uDDED',
  'INR': '\uD83C\uDDEE\uD83C\uDDF3',
  'AED': '\uD83C\uDDE6\uD83C\uDDEA',
  'ZAR': '\uD83C\uDDFF\uD83C\uDDE6'
};

@Injectable({
  providedIn: 'root'
})
export class CurrencyService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private readonly CACHE_KEY = 'currency_rates_cache';
  private readonly CACHE_DURATION = 24 * 60 * 60 * 1000; // 24 hours in ms
  private readonly API_URL = 'https://api.currencyapi.com/v3/latest';
  private readonly API_KEY = 'cur_live_tEyYYCmoeKRhKwj0LQ7g8CO8bnrX7HPRARFxBLhb';

  /**
   * Get exchange rates for a base currency
   */
  getRates(baseCurrency: string = 'ZAR'): Observable<ExchangeRates | null> {
    if (!isPlatformBrowser(this.platformId)) {
      return of(null);
    }

    // Check cache first
    const cached = this.getCachedRates(baseCurrency);
    if (cached) {
      return of(cached);
    }

    // Fetch fresh rates from currencyapi.com
    const targetCurrencies = SUPPORTED_CURRENCIES
      .map(c => c.code)
      .filter(c => c !== baseCurrency)
      .join(',');

    return this.http.get<{ data: Record<string, { code: string; value: number }> }>(
      `${this.API_URL}?apikey=${this.API_KEY}&base_currency=${baseCurrency}&currencies=${targetCurrencies}`
    ).pipe(
      map(response => {
        // Convert currencyapi.com format to our format
        const rates: Record<string, number> = {};
        Object.keys(response.data).forEach(code => {
          rates[code] = response.data[code].value;
        });

        const data: ExchangeRates = {
          base: baseCurrency,
          rates: rates,
          lastUpdated: new Date()
        };
        return data;
      }),
      tap(data => {
        this.cacheRates(baseCurrency, data);
      }),
      catchError(error => {
        console.error('Currency service error:', error);
        return of(null);
      })
    );
  }

  /**
   * Convert amount from one currency to another
   */
  convert(amount: number, rates: ExchangeRates, toCurrency: string): number {
    if (!rates || !rates.rates[toCurrency]) {
      return 0;
    }
    return amount * rates.rates[toCurrency];
  }

  /**
   * Format currency amount
   */
  formatCurrency(amount: number, currencyCode: string): string {
    const currency = SUPPORTED_CURRENCIES.find(c => c.code === currencyCode);
    const symbol = currency?.symbol || currencyCode;

    // Format based on currency conventions
    if (currencyCode === 'JPY') {
      return `${symbol}${Math.round(amount).toLocaleString()}`;
    }

    return `${symbol}${amount.toFixed(2)}`;
  }

  /**
   * Get flag emoji for currency
   */
  getCurrencyFlag(currencyCode: string): string {
    return currencyFlags[currencyCode] || '\uD83C\uDFF3\uFE0F';
  }

  /**
   * Get supported currencies list
   */
  getSupportedCurrencies(): CurrencyInfo[] {
    return SUPPORTED_CURRENCIES;
  }

  /**
   * Get cached rates
   */
  private getCachedRates(baseCurrency: string): ExchangeRates | null {
    if (!isPlatformBrowser(this.platformId)) return null;

    try {
      const cacheKey = `${this.CACHE_KEY}_${baseCurrency}`;
      const cached = localStorage.getItem(cacheKey);
      if (!cached) return null;

      const data = JSON.parse(cached);
      const cacheTime = new Date(data.lastUpdated).getTime();

      if (Date.now() - cacheTime < this.CACHE_DURATION) {
        data.lastUpdated = new Date(data.lastUpdated);
        return data;
      }

      // Cache expired
      localStorage.removeItem(cacheKey);
      return null;
    } catch {
      return null;
    }
  }

  /**
   * Cache rates
   */
  private cacheRates(baseCurrency: string, data: ExchangeRates): void {
    if (!isPlatformBrowser(this.platformId)) return;

    try {
      const cacheKey = `${this.CACHE_KEY}_${baseCurrency}`;
      localStorage.setItem(cacheKey, JSON.stringify(data));
    } catch {
      // Ignore storage errors
    }
  }

  /**
   * Get time since last update
   */
  getTimeSinceUpdate(lastUpdated: Date): string {
    const now = new Date();
    const diffMs = now.getTime() - lastUpdated.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);

    if (diffMins < 60) {
      return `${diffMins} min${diffMins !== 1 ? 's' : ''} ago`;
    } else if (diffHours < 24) {
      return `${diffHours} hour${diffHours !== 1 ? 's' : ''} ago`;
    } else {
      return `${Math.floor(diffHours / 24)} day${Math.floor(diffHours / 24) !== 1 ? 's' : ''} ago`;
    }
  }
}
