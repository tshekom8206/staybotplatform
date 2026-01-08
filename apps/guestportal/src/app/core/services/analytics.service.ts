import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { isPlatformBrowser } from '@angular/common';
import { filter } from 'rxjs/operators';
import { TenantService, TenantInfo } from './tenant.service';
import { environment } from '../../../environments/environment';

// Declare gtag function for TypeScript
declare global {
  interface Window {
    dataLayer: any[];
    gtag: (...args: any[]) => void;
  }
}

@Injectable({
  providedIn: 'root'
})
export class AnalyticsService {
  private tenantService = inject(TenantService);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);
  private initialized = false;

  /**
   * Initialize GA4 with tenant context
   * Should be called after tenant info is loaded
   */
  initialize(): void {
    if (!isPlatformBrowser(this.platformId) || this.initialized) return;

    const ga4Config = (environment as any).ga4;
    if (!ga4Config?.enabled || !ga4Config?.measurementId) {
      console.log('GA4 analytics disabled or not configured');
      return;
    }

    const tenant = this.tenantService.getCurrentTenant();
    if (!tenant) {
      console.warn('Cannot initialize GA4: No tenant context');
      return;
    }

    // Configure GA4 with tenant context as custom dimensions
    if (typeof window.gtag === 'function') {
      window.gtag('config', ga4Config.measurementId, {
        send_page_view: false, // We'll handle page views manually
        tenant_id: tenant.id.toString(),
        tenant_slug: tenant.slug,
        tenant_name: tenant.name
      });

      this.setupRouteTracking(tenant);
      this.initialized = true;
      console.log(`GA4 initialized for tenant: ${tenant.slug}`);
    } else {
      console.warn('gtag function not available');
    }
  }

  /**
   * Track a custom event with tenant context
   */
  trackEvent(eventName: string, params: Record<string, any> = {}): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const ga4Config = (environment as any).ga4;
    if (!ga4Config?.enabled) return;

    const tenant = this.tenantService.getCurrentTenant();
    if (typeof window.gtag === 'function') {
      window.gtag('event', eventName, {
        ...params,
        tenant_id: tenant?.id?.toString(),
        tenant_slug: tenant?.slug
      });
    }
  }

  /**
   * Track menu item clicks on home page
   */
  trackMenuClick(menuItem: string): void {
    this.trackEvent('menu_click', { menu_item: menuItem });
  }

  /**
   * Track maintenance request submissions
   */
  trackMaintenanceRequest(issues: string[], roomNumber?: string): void {
    this.trackEvent('maintenance_request', {
      issues: issues.join(','),
      room_number: roomNumber || 'unknown',
      issue_count: issues.length
    });
  }

  /**
   * Track rating submissions
   */
  trackRatingSubmitted(ratingValue: number, hasComment: boolean): void {
    this.trackEvent('rating_submitted', {
      rating_value: ratingValue,
      has_comment: hasComment
    });
  }

  /**
   * Track lost item reports
   */
  trackLostItemReport(category: string): void {
    this.trackEvent('lost_item_report', { category });
  }

  /**
   * Track service requests
   */
  trackServiceRequest(serviceName: string, serviceId?: number): void {
    this.trackEvent('service_request', {
      service_name: serviceName,
      service_id: serviceId?.toString()
    });
  }

  /**
   * Track WhatsApp opens
   */
  trackWhatsAppOpened(): void {
    this.trackEvent('whatsapp_opened');
  }

  /**
   * Track featured service/upsell clicks
   */
  trackFeaturedServiceClick(serviceName: string, price?: string | number): void {
    this.trackEvent('featured_service_click', {
      service_name: serviceName,
      price: price?.toString()
    });
  }

  /**
   * Set up automatic page view tracking on route changes
   */
  private setupRouteTracking(tenant: TenantInfo): void {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd)
    ).subscribe((event: NavigationEnd) => {
      this.trackPageView(event.urlAfterRedirects, document.title, tenant);
    });

    // Track initial page view
    this.trackPageView(this.router.url, document.title, tenant);
  }

  /**
   * Track page view event
   */
  private trackPageView(pagePath: string, pageTitle: string, tenant: TenantInfo): void {
    if (typeof window.gtag === 'function') {
      window.gtag('event', 'page_view', {
        page_path: pagePath,
        page_title: pageTitle,
        page_location: window.location.href,
        tenant_id: tenant.id.toString(),
        tenant_slug: tenant.slug
      });
    }
  }
}
