import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { filter, take } from 'rxjs/operators';
import { LanguageService } from './core/i18n/language.service';
import { TenantService } from './core/services/tenant.service';
import { AnalyticsService } from './core/services/analytics.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`,
  styles: []
})
export class AppComponent implements OnInit {
  // Inject LanguageService to ensure translations are initialized early
  private languageService = inject(LanguageService);
  private tenantService = inject(TenantService);
  private analyticsService = inject(AnalyticsService);

  ngOnInit(): void {
    // Initialize GA4 analytics after tenant info is loaded
    this.tenantService.tenant$.pipe(
      filter(tenant => !!tenant),
      take(1)
    ).subscribe(() => {
      this.analyticsService.initialize();
    });
  }
}
