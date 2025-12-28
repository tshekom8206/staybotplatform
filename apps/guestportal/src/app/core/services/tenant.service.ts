import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TenantInfo {
  id: number;
  name: string;
  slug: string;
  logoUrl?: string;
  backgroundImageUrl?: string;
  themePrimary?: string;
  phone?: string;
  whatsappNumber?: string;
  socialLinks?: {
    facebook?: string;
    instagram?: string;
    twitter?: string;
    website?: string;
  };
}

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  private http = inject(HttpClient);
  private _tenant$ = new BehaviorSubject<TenantInfo | null>(null);
  private _loading$ = new BehaviorSubject<boolean>(true);
  private _error$ = new BehaviorSubject<string | null>(null);

  public tenant$ = this._tenant$.asObservable();
  public loading$ = this._loading$.asObservable();
  public error$ = this._error$.asObservable();

  /**
   * Get tenant slug from subdomain
   * e.g., "panoramaview" from "panoramaview.staybot.co.za"
   */
  getTenantSlug(): string {
    const host = window.location.hostname;

    // For local development, use a default tenant
    if (host === 'localhost' || host === '127.0.0.1') {
      return 'panoramaview'; // Default tenant for development
    }

    // Extract subdomain from hostname
    const parts = host.split('.');
    if (parts.length >= 3) {
      return parts[0];
    }

    return 'panoramaview'; // Fallback
  }

  /**
   * Load tenant info from API
   */
  loadTenantInfo(): Observable<TenantInfo | null> {
    const slug = this.getTenantSlug();
    this._loading$.next(true);
    this._error$.next(null);

    return this.http.get<TenantInfo>(`${environment.apiUrl}/api/public/${slug}/info`).pipe(
      tap(tenant => {
        this._tenant$.next(tenant);
        this._loading$.next(false);
        this.applyTheme(tenant);
      }),
      catchError(error => {
        console.error('Failed to load tenant info:', error);
        // Use mock tenant for development when API is unavailable
        if (!environment.production) {
          const mockTenant: TenantInfo = {
            id: 1,
            name: 'Panorama View Guest House',
            slug: 'panoramaview',
            logoUrl: undefined,
            backgroundImageUrl: 'https://images.unsplash.com/photo-1566073771259-6a8506099945?w=1200&q=80',
            themePrimary: '#1976d2',
            phone: '+27123456789',
            whatsappNumber: '+27123456789',
            socialLinks: {
              facebook: 'https://facebook.com',
              instagram: 'https://instagram.com'
            }
          };
          this._tenant$.next(mockTenant);
          this._loading$.next(false);
          this.applyTheme(mockTenant);
          return of(mockTenant);
        }
        this._error$.next('Failed to load hotel information');
        this._loading$.next(false);
        return of(null);
      })
    );
  }

  /**
   * Apply tenant theme colors to CSS variables
   */
  private applyTheme(tenant: TenantInfo): void {
    if (tenant.themePrimary) {
      document.documentElement.style.setProperty('--theme-primary', tenant.themePrimary);
    }
  }

  /**
   * Get current tenant synchronously
   */
  getCurrentTenant(): TenantInfo | null {
    return this._tenant$.getValue();
  }

  /**
   * Open WhatsApp with hotel number
   */
  openWhatsApp(message?: string): void {
    const tenant = this.getCurrentTenant();
    if (!tenant?.whatsappNumber && !tenant?.phone) {
      console.error('No WhatsApp number available');
      return;
    }

    const phone = (tenant.whatsappNumber || tenant.phone || '').replace(/[^0-9]/g, '');
    const encodedMessage = message ? encodeURIComponent(message) : '';
    const url = `https://wa.me/${phone}${encodedMessage ? '?text=' + encodedMessage : ''}`;

    window.open(url, '_blank');
  }
}
