import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';

export interface HotelInformation {
  // Basic Information
  name: string;
  description: string;
  category: string; // 5-star, 4-star, boutique, etc.
  logo?: string;

  // Contact Information
  phone: string;
  email: string;
  website: string;

  // Address
  address: {
    street: string;
    city: string;
    state: string;
    postalCode: string;
    country: string;
    latitude?: number;
    longitude?: number;
  };

  // Business Details
  checkInTime: string;
  checkOutTime: string;
  numberOfRooms: number;
  numberOfFloors: number;
  establishedYear: number;

  // Languages
  supportedLanguages: string[];
  defaultLanguage: string;

  // Features
  features: string[];

  // Social Media
  socialMedia: {
    facebook?: string;
    twitter?: string;
    instagram?: string;
    linkedin?: string;
  };

  // Policies
  policies: {
    cancellationPolicy: string;
    petPolicy: string;
    smokingPolicy: string;
    childPolicy: string;
  };

  // Configuration Settings
  settings: {
    allowOnlineBooking: boolean;
    requirePhoneVerification: boolean;
    enableNotifications: boolean;
    enableChatbot: boolean;
    timezone: string;
    currency: string;
  };

  // WiFi Credentials
  wifi?: {
    network?: string;
    password?: string;
  };

  // Room Configuration (from Tenant)
  validRooms?: string;
}

export interface UpdateHotelInfoRequest {
  name?: string;
  description?: string;
  category?: string;
  logo?: string;
  phone?: string;
  email?: string;
  website?: string;
  address?: {
    street?: string;
    city?: string;
    state?: string;
    postalCode?: string;
    country?: string;
    latitude?: number;
    longitude?: number;
  };
  checkInTime?: string;
  checkOutTime?: string;
  numberOfRooms?: number;
  numberOfFloors?: number;
  establishedYear?: number;
  supportedLanguages?: string[];
  defaultLanguage?: string;
  features?: string[];
  socialMedia?: {
    facebook?: string;
    twitter?: string;
    instagram?: string;
    linkedin?: string;
  };
  policies?: {
    cancellationPolicy?: string;
    petPolicy?: string;
    smokingPolicy?: string;
    childPolicy?: string;
  };
  settings?: {
    allowOnlineBooking?: boolean;
    requirePhoneVerification?: boolean;
    enableNotifications?: boolean;
    enableChatbot?: boolean;
    timezone?: string;
    currency?: string;
  };
  wifi?: {
    network?: string;
    password?: string;
  };

  // Room Configuration
  validRooms?: string;
}

@Injectable({
  providedIn: 'root'
})
export class HotelInfoService {
  private apiService = inject(ApiService);
  private authService = inject(AuthService);

  /**
   * Get hotel information for current tenant
   */
  getHotelInfo(): Observable<HotelInformation> {
    const currentTenant = this.authService.currentTenantValue;
    if (!currentTenant) {
      return throwError(() => new Error('No tenant selected'));
    }

    return this.apiService.get<{ tenant: HotelInformation }>(`tenant/${currentTenant.id}`)
      .pipe(
        map(response => response.tenant),
        catchError(error => {
          console.error('Error loading hotel information:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Update hotel information for current tenant
   */
  updateHotelInfo(hotelInfo: UpdateHotelInfoRequest): Observable<HotelInformation> {
    const currentTenant = this.authService.currentTenantValue;
    if (!currentTenant) {
      return throwError(() => new Error('No tenant selected'));
    }

    return this.apiService.put<{ tenant: HotelInformation }>(`tenant/${currentTenant.id}`, hotelInfo)
      .pipe(
        map(response => response.tenant),
        catchError(error => {
          console.error('Error updating hotel information:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available hotel categories from the API
   */
  getHotelCategories(): Observable<Array<{value: string, label: string}>> {
    return this.apiService.get<{categories: Array<{value: string, label: string}>}>('tenant/categories')
      .pipe(
        map(response => response.categories),
        catchError(error => {
          console.error('Error loading hotel categories:', error);
          // Return default categories as fallback
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available languages from the API
   */
  getAvailableLanguages(): Observable<Array<{code: string, name: string}>> {
    return this.apiService.get<{languages: Array<{code: string, name: string}>}>('tenant/languages')
      .pipe(
        map(response => response.languages),
        catchError(error => {
          console.error('Error loading available languages:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available features from the API (now redirects to Services table)
   */
  getAvailableFeatures(): Observable<string[]> {
    const currentTenant = this.authService.currentTenantValue;
    if (!currentTenant) {
      return throwError(() => new Error('No tenant selected'));
    }

    return this.apiService.get<{features: string[]}>(`tenant/${currentTenant.id}/features`)
      .pipe(
        map(response => response.features),
        catchError(error => {
          console.error('Error loading available features:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available timezones from the API
   */
  getTimezones(): Observable<string[]> {
    return this.apiService.get<{timezones: string[]}>('tenant/timezones')
      .pipe(
        map(response => response.timezones),
        catchError(error => {
          console.error('Error loading timezones:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available currencies from the API
   */
  getCurrencies(): Observable<Array<{code: string, name: string}>> {
    return this.apiService.get<{currencies: Array<{code: string, name: string}>}>('tenant/currencies')
      .pipe(
        map(response => response.currencies),
        catchError(error => {
          console.error('Error loading currencies:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Toggle service availability in the Services table
   */
  toggleServiceAvailability(serviceName: string, isAvailable: boolean): Observable<any> {
    const currentTenant = this.authService.currentTenantValue;
    if (!currentTenant) {
      return throwError(() => new Error('No tenant selected'));
    }

    // This will require a new API endpoint to update Services table
    // For now, we'll use the existing services API structure
    return this.apiService.patch(`services/toggle-availability`, {
      tenantId: currentTenant.id,
      serviceName,
      isAvailable
    }).pipe(
      catchError(error => {
        console.error('Error toggling service availability:', error);
        return throwError(() => error);
      })
    );
  }
}