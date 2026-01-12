import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, catchError, of } from 'rxjs';
import { isPlatformBrowser } from '@angular/common';
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
  private platformId = inject(PLATFORM_ID);
  private _tenant$ = new BehaviorSubject<TenantInfo | null>(null);
  private _loading$ = new BehaviorSubject<boolean>(true);
  private _error$ = new BehaviorSubject<string | null>(null);

  public tenant$ = this._tenant$.asObservable();
  public loading$ = this._loading$.asObservable();
  public error$ = this._error$.asObservable();

  /**
   * Get tenant slug from subdomain or query parameter
   * e.g., "panoramaview" from "panoramaview.staybot.co.za"
   * or from "?tenant=panoramaview" query parameter for testing
   */
  getTenantSlug(): string {
    // Check for query parameter first (for testing)
    const urlParams = new URLSearchParams(window.location.search);
    const tenantParam = urlParams.get('tenant');
    if (tenantParam) {
      // Store in sessionStorage for subsequent page loads
      sessionStorage.setItem('tenant_slug', tenantParam);
      return tenantParam;
    }

    // Check sessionStorage (for navigation within the app)
    const storedTenant = sessionStorage.getItem('tenant_slug');
    if (storedTenant) {
      return storedTenant;
    }

    const host = window.location.hostname;

    // For local development, use a default tenant
    if (host === 'localhost' || host === '127.0.0.1') {
      return 'riboville'; // Default tenant for development
    }

    // For Azure test deployment (staybot-guest.azurewebsites.net)
    if (host.includes('azurewebsites.net')) {
      return 'riboville'; // Default tenant for Azure testing
    }

    // Extract subdomain from hostname (e.g., panoramaview.staybot.co.za)
    const parts = host.split('.');
    if (parts.length >= 3) {
      return parts[0];
    }

    return 'riboville'; // Fallback
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
        this.setupPWABranding(tenant);
      }),
      catchError(error => {
        console.error('Failed to load tenant info:', error);
        // Use mock tenant for development when API is unavailable
        if (!environment.production) {
          const mockTenant: TenantInfo = {
            id: 1,
            name: 'Riboville Hotel',
            slug: 'riboville',
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
    if (!isPlatformBrowser(this.platformId)) return;

    if (tenant.themePrimary) {
      document.documentElement.style.setProperty('--theme-primary', tenant.themePrimary);
    }
  }

  /**
   * Setup PWA branding with tenant logo and info
   */
  private setupPWABranding(tenant: TenantInfo): void {
    if (!isPlatformBrowser(this.platformId)) return;

    // Set dynamic favicon using tenant logo
    if (tenant.logoUrl) {
      this.setDynamicFavicon(tenant.logoUrl);
    }

    // Set dynamic manifest with tenant branding (uses Blob URL for same-origin)
    this.setDynamicManifest(tenant);

    // Update meta tags
    this.updateMetaTags(tenant);
  }

  /**
   * Dynamically set favicon to tenant logo
   */
  private setDynamicFavicon(logoUrl: string): void {
    if (!isPlatformBrowser(this.platformId)) return;

    // Remove existing favicons
    const existingFavicons = document.querySelectorAll("link[rel*='icon']");
    existingFavicons.forEach(el => el.remove());

    // Add new favicon with tenant logo
    const link = document.createElement('link');
    link.rel = 'icon';
    link.type = 'image/png';
    link.href = logoUrl;
    document.head.appendChild(link);

    // Also set apple-touch-icon for iOS
    const appleLink = document.createElement('link');
    appleLink.rel = 'apple-touch-icon';
    appleLink.href = logoUrl;
    document.head.appendChild(appleLink);
  }

  /**
   * Set dynamic manifest using Blob URL (same-origin for PWA compatibility)
   */
  private setDynamicManifest(tenant: TenantInfo): void {
    if (!isPlatformBrowser(this.platformId)) return;

    // Remove existing manifest
    const existingManifest = document.querySelector("link[rel='manifest']");
    if (existingManifest) {
      existingManifest.remove();
    }

    // Create manifest object with tenant branding
    const logoUrl = tenant.logoUrl || `${window.location.origin}/favicon.ico`;
    const themePrimary = tenant.themePrimary || '#1976d2';
    const shortName = tenant.name.length > 12 ? tenant.name.substring(0, 12) : tenant.name;
    const baseUrl = window.location.origin;

    // Ensure logo URL is absolute
    const absoluteLogoUrl = logoUrl.startsWith('http') ? logoUrl : `${baseUrl}${logoUrl.startsWith('/') ? '' : '/'}${logoUrl}`;

    const manifest = {
      name: tenant.name,
      short_name: shortName,
      description: `Guest services for ${tenant.name}`,
      start_url: baseUrl + '/',
      scope: baseUrl + '/',
      display: 'standalone',
      orientation: 'portrait-primary',
      theme_color: themePrimary,
      background_color: '#FFFFFF',
      categories: ['travel', 'hospitality', 'lifestyle'],
      icons: [
        // Use 'any' purpose for general icons
        { src: absoluteLogoUrl, sizes: '192x192', type: 'image/png', purpose: 'any' },
        { src: absoluteLogoUrl, sizes: '512x512', type: 'image/png', purpose: 'any' },
        // Separate maskable icons
        { src: absoluteLogoUrl, sizes: '192x192', type: 'image/png', purpose: 'maskable' },
        { src: absoluteLogoUrl, sizes: '512x512', type: 'image/png', purpose: 'maskable' }
      ]
    };

    // Create Blob URL for manifest (same-origin)
    const blob = new Blob([JSON.stringify(manifest)], { type: 'application/manifest+json' });
    const manifestUrl = URL.createObjectURL(blob);

    // Add new manifest link
    const link = document.createElement('link');
    link.rel = 'manifest';
    link.href = manifestUrl;
    document.head.appendChild(link);
  }

  /**
   * Update meta tags with tenant info
   */
  private updateMetaTags(tenant: TenantInfo): void {
    if (!isPlatformBrowser(this.platformId)) return;

    // Update apple-mobile-web-app-title
    let appTitle = document.querySelector("meta[name='apple-mobile-web-app-title']");
    if (appTitle) {
      appTitle.setAttribute('content', tenant.name);
    } else {
      appTitle = document.createElement('meta');
      appTitle.setAttribute('name', 'apple-mobile-web-app-title');
      appTitle.setAttribute('content', tenant.name);
      document.head.appendChild(appTitle);
    }

    // Update theme-color if tenant has custom color
    if (tenant.themePrimary) {
      let themeColor = document.querySelector("meta[name='theme-color']");
      if (themeColor) {
        themeColor.setAttribute('content', tenant.themePrimary);
      }
    }

    // Update page title
    document.title = tenant.name;

    // Update description
    let description = document.querySelector("meta[name='description']");
    if (description) {
      description.setAttribute('content', `Guest services for ${tenant.name}`);
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
