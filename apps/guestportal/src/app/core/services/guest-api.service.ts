import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { TenantService } from './tenant.service';
import { environment } from '../../../environments/environment';

// DTOs
export interface HotelInfo {
  id: number;
  name: string;
  slug: string;
  logoUrl?: string;
  themeColor: string;
  phone?: string;
  email?: string;
  address?: string;
  checkInTime?: string;
  checkOutTime?: string;
  socialLinks: {
    facebook?: string;
    instagram?: string;
    twitter?: string;
    website?: string;
  };
}

export interface MenuCategory {
  id: number;
  name: string;
  description?: string;
  mealType: string;
  icon: string;
  items: MenuItem[];
}

export interface MenuItem {
  id: number;
  name: string;
  description: string;
  price: string;
  priceCents: number;
  currency: string;
  imageUrl?: string;
  allergens?: string;
  isVegetarian: boolean;
  isVegan: boolean;
  isGlutenFree: boolean;
  isSpicy: boolean;
  isSpecial: boolean;
  isPopular?: boolean;
  isChefPick?: boolean;
  hasVegetarianOption?: boolean;
  isCustomizable?: boolean;
  customizeOptions?: string;
  tags?: string[];
}

export interface ServiceCategory {
  category: string;
  icon: string;
  services: ServiceItem[];
}

export interface ServiceItem {
  id: number;
  name: string;
  description?: string;
  category?: string;
  isChargeable: boolean;
  price: string;
  currency?: string;
  pricingUnit?: string;
  availableHours?: string;
  contactInfo?: string;
  requiresBooking: boolean;
  advanceBookingHours?: number;
}

export interface MaintenanceCategory {
  id: string;
  name: string;
  icon: string;
  description: string;
}

export interface MaintenanceRequest {
  roomNumber: string;
  issues: string[];
  description?: string;
}

export interface MaintenanceResponse {
  success: boolean;
  message: string;
  taskId: number;
  estimatedResponse: string;
}

export interface FoundItem {
  id: number;
  itemName: string;
  category: string;
  description?: string;
  color?: string;
  brand?: string;
  locationFound?: string;
  foundDate: string;
}

export interface LostItemRequest {
  itemName: string;
  category?: string;
  description?: string;
  color?: string;
  brand?: string;
  locationLost?: string;
  guestName?: string;
  phone?: string;
  roomNumber?: string;
}

export interface LostItemResponse {
  success: boolean;
  message: string;
  reportId: number;
}

export interface RatingRequest {
  rating: number;
  comment?: string;
  roomNumber?: string;
  guestName?: string;
  phone?: string;
}

export interface RatingResponse {
  success: boolean;
  message: string;
}

export interface GuestPromise {
  title: string;
  content: string;
}

export interface SupportedLanguage {
  code: string;
  name: string;
  nativeName: string;
  flag: string;
}

@Injectable({
  providedIn: 'root'
})
export class GuestApiService {
  private http = inject(HttpClient);
  private tenantService = inject(TenantService);

  private get apiUrl(): string {
    const slug = this.tenantService.getTenantSlug();
    return `${environment.apiUrl}/api/public/${slug}`;
  }

  // Hotel Info
  getHotelInfo(): Observable<HotelInfo> {
    return this.http.get<HotelInfo>(`${this.apiUrl}/info`).pipe(
      catchError(error => {
        console.error('Error fetching hotel info:', error);
        return of({
          id: 0,
          name: 'Guest Portal',
          slug: 'default',
          themeColor: '#1976d2',
          socialLinks: {}
        });
      })
    );
  }

  // Guest Promise
  getGuestPromise(): Observable<GuestPromise> {
    return this.http.get<GuestPromise>(`${this.apiUrl}/guest-promise`).pipe(
      catchError(error => {
        console.error('Error fetching guest promise:', error);
        return of({
          title: 'Our Guest Promise',
          content: '{"promises": ["Your comfort is our priority", "24/7 concierge at your fingertips", "Personalized service, every stay", "We listen, we act, we care"]}'
        });
      })
    );
  }

  // Menu
  getMenu(mealType?: string): Observable<{ categories: MenuCategory[] }> {
    const options = mealType ? { params: { mealType } } : {};
    return this.http.get<{ categories: MenuCategory[] }>(`${this.apiUrl}/menu`, options).pipe(
      catchError(error => {
        console.error('Error fetching menu:', error);
        return of({ categories: [] as MenuCategory[] });
      })
    );
  }

  getMenuCategory(categoryId: number): Observable<MenuCategory | null> {
    return this.http.get<MenuCategory>(`${this.apiUrl}/menu/category/${categoryId}`).pipe(
      catchError(error => {
        console.error('Error fetching menu category:', error);
        return of(null);
      })
    );
  }

  // Services/Amenities
  getServices(category?: string): Observable<{ categories: ServiceCategory[] }> {
    const options = category ? { params: { category } } : {};
    return this.http.get<{ categories: ServiceCategory[] }>(`${this.apiUrl}/services`, options).pipe(
      catchError(error => {
        console.error('Error fetching services:', error);
        return of({ categories: [] as ServiceCategory[] });
      })
    );
  }

  // Maintenance
  getMaintenanceCategories(): Observable<{ categories: MaintenanceCategory[] }> {
    return this.http.get<{ categories: MaintenanceCategory[] }>(`${this.apiUrl}/maintenance-categories`).pipe(
      catchError(error => {
        console.error('Error fetching maintenance categories:', error);
        return of({ categories: [] as MaintenanceCategory[] });
      })
    );
  }

  submitMaintenanceRequest(request: MaintenanceRequest): Observable<MaintenanceResponse> {
    return this.http.post<MaintenanceResponse>(`${this.apiUrl}/maintenance`, request).pipe(
      catchError(error => {
        console.error('Error submitting maintenance request:', error);
        return of({
          success: false,
          message: 'Failed to submit request. Please try again.',
          taskId: 0,
          estimatedResponse: ''
        });
      })
    );
  }

  // Lost & Found
  getFoundItems(category?: string): Observable<{ items: FoundItem[] }> {
    const options = category ? { params: { category } } : {};
    return this.http.get<{ items: FoundItem[] }>(`${this.apiUrl}/lost-found`, options).pipe(
      catchError(error => {
        console.error('Error fetching found items:', error);
        return of({ items: [] as FoundItem[] });
      })
    );
  }

  reportLostItem(request: LostItemRequest): Observable<LostItemResponse> {
    return this.http.post<LostItemResponse>(`${this.apiUrl}/lost-found`, request).pipe(
      catchError(error => {
        console.error('Error reporting lost item:', error);
        return of({
          success: false,
          message: 'Failed to submit report. Please try again.',
          reportId: 0
        });
      })
    );
  }

  // Rating
  submitRating(request: RatingRequest): Observable<RatingResponse> {
    return this.http.post<RatingResponse>(`${this.apiUrl}/rating`, request).pipe(
      catchError(error => {
        console.error('Error submitting rating:', error);
        return of({
          success: false,
          message: 'Failed to submit rating. Please try again.'
        });
      })
    );
  }

  // Languages
  getLanguages(): Observable<{ languages: SupportedLanguage[] }> {
    return this.http.get<{ languages: SupportedLanguage[] }>(`${this.apiUrl}/languages`).pipe(
      catchError(error => {
        console.error('Error fetching languages:', error);
        const defaultLanguages: SupportedLanguage[] = [
          { code: 'en', name: 'English', nativeName: 'English', flag: 'GB' },
          { code: 'af', name: 'Afrikaans', nativeName: 'Afrikaans', flag: 'ZA' },
          { code: 'zu', name: 'Zulu', nativeName: 'isiZulu', flag: 'ZA' }
        ];
        return of({ languages: defaultLanguages });
      })
    );
  }
}
