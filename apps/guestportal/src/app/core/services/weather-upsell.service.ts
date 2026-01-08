import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { TenantService } from './tenant.service';
import { environment } from '../../../environments/environment';

export interface WeatherUpsellService {
  id: number;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  imageUrl?: string;
  isChargeable: boolean;
  price?: string;
  priceAmount?: number;
  currency?: string;
  pricingUnit?: string;
  availableHours?: string;
  requiresBooking: boolean;
  advanceBookingHours?: number;
}

export interface WeatherUpsell {
  bannerText: string | null;
  bannerIcon: string | null;
  weatherCondition: string | null;
  services: WeatherUpsellService[];
}

@Injectable({
  providedIn: 'root'
})
export class WeatherUpsellApiService {
  private http = inject(HttpClient);
  private tenantService = inject(TenantService);

  private get apiUrl(): string {
    const slug = this.tenantService.getTenantSlug();
    return `${environment.apiUrl}/api/public/${slug}`;
  }

  /**
   * Get weather-based upsell recommendations
   * @param temperature Current temperature in Celsius
   * @param weatherCode WMO weather code
   */
  getWeatherUpsells(temperature: number, weatherCode: number): Observable<WeatherUpsell | null> {
    return this.http.get<WeatherUpsell>(
      `${this.apiUrl}/weather-upsells?temperature=${temperature}&weatherCode=${weatherCode}`
    ).pipe(
      map(response => {
        // Only return if there are services to show
        if (response.services && response.services.length > 0) {
          return response;
        }
        return null;
      }),
      catchError(error => {
        console.error('Error fetching weather upsells:', error);
        return of(null);
      })
    );
  }
}
