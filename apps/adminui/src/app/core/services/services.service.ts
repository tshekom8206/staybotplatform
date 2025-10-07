import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';

export interface HotelService {
  id: number;
  name: string;
  description?: string;
  category: string;
  icon?: string;
  isAvailable: boolean;
  isChargeable: boolean;
  price?: number;
  currency?: string;
  pricingUnit?: string;
  availableHours?: string;
  contactMethod?: string;
  contactInfo?: string;
  priority: number;
  specialInstructions?: string;
  imageUrl?: string;
  requiresAdvanceBooking: boolean;
  advanceBookingHours?: number;
  createdAt: Date;
  updatedAt: Date;
}

export interface ServiceCategory {
  value: string;
  label: string;
  description?: string;
}

export interface ServiceIcon {
  name: string;
  icon: string;
  label?: string;
}

export interface CreateServiceRequest {
  name: string;
  description?: string;
  category: string;
  icon?: string;
  isAvailable: boolean;
  isChargeable: boolean;
  price?: number;
  currency?: string;
  pricingUnit?: string;
  availableHours?: string;
  contactMethod?: string;
  contactInfo?: string;
  priority: number;
  specialInstructions?: string;
  imageUrl?: string;
  requiresAdvanceBooking: boolean;
  advanceBookingHours?: number;
}

export interface UpdateServiceRequest {
  name: string;
  description?: string;
  category: string;
  icon?: string;
  isAvailable: boolean;
  isChargeable: boolean;
  price?: number;
  currency?: string;
  pricingUnit?: string;
  availableHours?: string;
  contactMethod?: string;
  contactInfo?: string;
  priority: number;
  specialInstructions?: string;
  imageUrl?: string;
  requiresAdvanceBooking: boolean;
  advanceBookingHours?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ServicesService {
  private apiService = inject(ApiService);
  private authService = inject(AuthService);

  /**
   * Get all services for the current tenant
   */
  getServices(): Observable<HotelService[]> {
    return this.apiService.get<{ services: HotelService[] }>('services')
      .pipe(
        map(response => response.services.map(service => ({
          ...service,
          createdAt: new Date(service.createdAt),
          updatedAt: new Date(service.updatedAt)
        }))),
        catchError(error => {
          console.error('Error loading services:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get a specific service by ID
   */
  getService(id: number): Observable<HotelService> {
    return this.apiService.get<{ service: HotelService }>(`services/${id}`)
      .pipe(
        map(response => ({
          ...response.service,
          createdAt: new Date(response.service.createdAt),
          updatedAt: new Date(response.service.updatedAt)
        })),
        catchError(error => {
          console.error('Error loading service:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Create a new service
   */
  createService(service: CreateServiceRequest): Observable<HotelService> {
    return this.apiService.post<{ service: HotelService }>('services', service)
      .pipe(
        map(response => ({
          ...response.service,
          createdAt: new Date(response.service.createdAt),
          updatedAt: new Date(response.service.updatedAt)
        })),
        catchError(error => {
          console.error('Error creating service:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Update an existing service
   */
  updateService(id: number, service: UpdateServiceRequest): Observable<HotelService> {
    return this.apiService.put<{ service: HotelService }>(`services/${id}`, service)
      .pipe(
        map(response => ({
          ...response.service,
          createdAt: new Date(response.service.createdAt),
          updatedAt: new Date(response.service.updatedAt)
        })),
        catchError(error => {
          console.error('Error updating service:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Delete a service
   */
  deleteService(id: number): Observable<void> {
    return this.apiService.delete<void>(`services/${id}`)
      .pipe(
        catchError(error => {
          console.error('Error deleting service:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available service categories
   */
  getServiceCategories(): Observable<ServiceCategory[]> {
    return this.apiService.get<{ categories: ServiceCategory[] }>('services/categories')
      .pipe(
        map(response => response.categories),
        catchError(error => {
          console.error('Error loading service categories:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available service icons
   */
  getServiceIcons(): Observable<ServiceIcon[]> {
    return this.apiService.get<{ icons: ServiceIcon[] }>('services/icons')
      .pipe(
        map(response => response.icons),
        catchError(error => {
          console.error('Error loading service icons:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available contact methods (hardcoded list)
   */
  getContactMethods(): string[] {
    return [
      'phone',
      'extension',
      'app',
      'front-desk',
      'concierge',
      'reception',
      'text',
      'email',
      'in-person'
    ];
  }

  /**
   * Get available pricing units (hardcoded list)
   */
  getPricingUnits(): string[] {
    return [
      'per service',
      'per hour',
      'per day',
      'per person',
      'per room',
      'per night',
      'per item',
      'flat rate',
      'per minute',
      'per visit'
    ];
  }

  /**
   * Get available currencies (from hotel info service)
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
}