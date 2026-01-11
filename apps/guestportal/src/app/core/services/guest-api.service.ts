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
  city?: string;
  latitude?: number;
  longitude?: number;
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
  icon?: string;
  imageUrl?: string;
  isChargeable: boolean;
  price: string;
  priceAmount?: number;
  currency?: string;
  pricingUnit?: string;
  availableHours?: string;
  contactInfo?: string;
  requiresBooking: boolean;
  advanceBookingHours?: number;
  timeSlots?: string;
}

export interface FeaturedService {
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
  timeSlots?: string;
}

export interface ServiceRequest {
  serviceId: number;
  roomNumber: string;
  preferredTime?: string;
  specialRequests?: string;
  guestName?: string;
  phone?: string;
  source?: string; // For upsell tracking: weather_warm, weather_hot, featured_carousel, etc.
}

export interface ServiceRequestResponse {
  success: boolean;
  message: string;
  taskId: number;
  serviceName: string;
  estimatedResponse: string;
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

export interface LostItemReport {
  id: number;
  itemName: string;
  category: string;
  description?: string;
  color?: string;
  brand?: string;
  locationLost?: string;
  reportedDate: string;
  status: 'searching' | 'found';
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

export interface HouseRules {
  smoking?: string;
  pets?: string;
  children?: string;
  cancellation?: string;
  checkInTime?: string;
  checkOutTime?: string;
}

export interface WifiCredentials {
  network?: string;
  password?: string;
}

export interface SupportedLanguage {
  code: string;
  name: string;
  nativeName: string;
  flag: string;
}

export interface RequestItem {
  id: number;
  name: string;
  category: string;
  description?: string;
  requiresQuantity: boolean;
  defaultQuantityLimit: number;
  estimatedTime?: number;
  icon: string;
}

export interface ItemRequest {
  requestItemId: number;
  roomNumber?: string;
  quantity?: number;
  notes?: string;
}

export interface ItemRequestResponse {
  success: boolean;
  message: string;
  taskId: number;
  itemName: string;
  estimatedTime: number;
}

// Guest Journey - Prepare Page
export interface PrepareItem {
  id: number;
  type: 'item' | 'service';
  name: string;
  description?: string;
  category?: string;
  price?: number;
  isChargeable: boolean;
  icon?: string;
  imageUrl?: string;
  currency?: string;
  pricingUnit?: string;
  requiresBooking?: boolean;
  estimatedTime?: number;
}

export interface PrepareItemsResponse {
  items: PrepareItem[];
  services: PrepareItem[];
}

// Guest Journey - Feedback Page
export interface FeedbackCategory {
  id: string;
  name: string;
  icon: string;
  description: string;
}

export interface QuickFeedbackRequest {
  rating: number;
  comment?: string;
  roomNumber?: string;
  issueCategory?: string;
}

export interface QuickFeedbackResponse {
  success: boolean;
  message: string;
}

// Guest Journey - Booking Info (for pre-arrival)
export interface BookingInfo {
  id: number;
  guestFirstName: string;
  guestName: string;
  roomNumber?: string;
  checkinDate: string;
  checkoutDate: string;
  status: string;
  hasCheckedIn: boolean;
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

  // House Rules
  getHouseRules(): Observable<HouseRules> {
    return this.http.get<HouseRules>(`${this.apiUrl}/house-rules`).pipe(
      catchError(error => {
        console.error('Error fetching house rules:', error);
        return of({});
      })
    );
  }

  // WiFi Credentials
  getWifiCredentials(): Observable<WifiCredentials> {
    return this.http.get<WifiCredentials>(`${this.apiUrl}/wifi`).pipe(
      catchError(error => {
        console.error('Error fetching WiFi credentials:', error);
        return of({});
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
  getLostReports(category?: string): Observable<{ items: LostItemReport[] }> {
    const options = category ? { params: { category } } : {};
    return this.http.get<{ items: LostItemReport[] }>(`${this.apiUrl}/lost-reports`, options).pipe(
      catchError(error => {
        console.error('Error fetching lost reports:', error);
        return of({ items: [] as LostItemReport[] });
      })
    );
  }

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

  // Featured Services (Upselling)
  getFeaturedServices(): Observable<{ services: FeaturedService[] }> {
    return this.http.get<{ services: FeaturedService[] }>(`${this.apiUrl}/services/featured`).pipe(
      catchError(error => {
        console.error('Error fetching featured services:', error);
        return of({ services: [] as FeaturedService[] });
      })
    );
  }

  // Contextual Services (Time-based recommendations)
  getContextualServices(timeSlot?: string): Observable<{ timeSlot: string; services: FeaturedService[] }> {
    const options = timeSlot ? { params: { timeSlot } } : {};
    return this.http.get<{ timeSlot: string; services: FeaturedService[] }>(`${this.apiUrl}/services/contextual`, options).pipe(
      catchError(error => {
        console.error('Error fetching contextual services:', error);
        return of({ timeSlot: 'unknown', services: [] as FeaturedService[] });
      })
    );
  }

  // Submit Service Request
  submitServiceRequest(request: ServiceRequest): Observable<ServiceRequestResponse> {
    return this.http.post<ServiceRequestResponse>(`${this.apiUrl}/services/request`, request).pipe(
      catchError(error => {
        console.error('Error submitting service request:', error);
        return of({
          success: false,
          message: 'Failed to submit request. Please try again.',
          taskId: 0,
          serviceName: '',
          estimatedResponse: ''
        });
      })
    );
  }

  // Request Items (Housekeeping)
  getRequestItems(category?: string, department?: string): Observable<{ items: RequestItem[] }> {
    const params: Record<string, string> = {};
    if (category) params['category'] = category;
    if (department) params['department'] = department;
    const options = Object.keys(params).length > 0 ? { params } : {};
    return this.http.get<{ items: RequestItem[] }>(`${this.apiUrl}/request-items`, options).pipe(
      catchError(error => {
        console.error('Error fetching request items:', error);
        return of({ items: [] as RequestItem[] });
      })
    );
  }

  submitItemRequest(request: ItemRequest): Observable<ItemRequestResponse> {
    return this.http.post<ItemRequestResponse>(`${this.apiUrl}/item-request`, request).pipe(
      catchError(error => {
        console.error('Error submitting item request:', error);
        return of({
          success: false,
          message: 'Failed to submit request. Please try again.',
          taskId: 0,
          itemName: '',
          estimatedTime: 0
        });
      })
    );
  }

  submitCustomRequest(request: {
    description: string;
    roomNumber?: string;
    timing?: string;
    department?: string;
    source?: string;
  }): Observable<{success: boolean; message: string; taskId: number}> {
    return this.http.post<{success: boolean; message: string; taskId: number}>(
      `${this.apiUrl}/custom-request`,
      {
        description: request.description,
        roomNumber: request.roomNumber,
        timing: request.timing,
        department: request.department || 'Concierge',
        source: request.source || 'guest_portal'
      }
    ).pipe(
      catchError(error => {
        console.error('Error submitting custom request:', error);
        return of({
          success: false,
          message: 'Failed to submit custom request. Please try again.',
          taskId: 0
        });
      })
    );
  }

  // Guest Journey - Prepare Page (Pre-Arrival Upsells)
  getPrepareItems(): Observable<PrepareItemsResponse> {
    return this.http.get<PrepareItemsResponse>(`${this.apiUrl}/prepare-items`).pipe(
      catchError(error => {
        console.error('Error fetching prepare items:', error);
        return of({ items: [], services: [] });
      })
    );
  }

  // Guest Journey - Feedback Categories
  getFeedbackCategories(): Observable<{ categories: FeedbackCategory[] }> {
    return this.http.get<{ categories: FeedbackCategory[] }>(`${this.apiUrl}/feedback-categories`).pipe(
      catchError(error => {
        console.error('Error fetching feedback categories:', error);
        return of({ categories: [] });
      })
    );
  }

  // Guest Journey - Submit Quick Feedback (Welcome Settled)
  submitQuickFeedback(request: QuickFeedbackRequest): Observable<QuickFeedbackResponse> {
    return this.http.post<QuickFeedbackResponse>(`${this.apiUrl}/feedback`, request).pipe(
      catchError(error => {
        console.error('Error submitting feedback:', error);
        return of({
          success: false,
          message: 'Failed to submit feedback. Please try again.'
        });
      })
    );
  }

  // Guest Journey - Get Booking Info (for pre-arrival prepare page)
  getBookingInfo(bookingId: number): Observable<BookingInfo | null> {
    return this.http.get<BookingInfo>(`${this.apiUrl}/booking/${bookingId}`).pipe(
      catchError(error => {
        console.error('Error fetching booking info:', error);
        return of(null);
      })
    );
  }
}
