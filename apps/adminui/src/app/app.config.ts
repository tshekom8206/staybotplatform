import { ApplicationConfig, importProvidersFrom, provideZoneChangeDetection, isDevMode, LOCALE_ID } from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeZa from '@angular/common/locales/en-ZA';

import { routes } from './app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { SweetAlert2Module } from '@sweetalert2/ngx-sweetalert2';
import { provideHighlightOptions } from 'ngx-highlightjs';

// Import interceptors
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { ErrorInterceptor } from './core/interceptors/error.interceptor';
import { provideServiceWorker } from '@angular/service-worker';

// Register South African locale
registerLocaleData(localeZa, 'en-ZA');

const highlightOptions = {
  coreLibraryLoader: () => import('highlight.js/lib/core'),
  languages: {
    typescript: () => import('highlight.js/lib/languages/typescript'),
    scss: () => import('highlight.js/lib/languages/scss'),
    xml: () => import('highlight.js/lib/languages/xml')
  },
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideAnimationsAsync(),
    provideHttpClient(
      withInterceptors([AuthInterceptor, ErrorInterceptor])
    ),
    importProvidersFrom([SweetAlert2Module.forRoot()]), // ngx-sweetalert2: https://github.com/sweetalert2/ngx-sweetalert2
    provideHighlightOptions(highlightOptions),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000'
    }), // ngx-highlightjs: https://github.com/murhafsousli/ngx-highlightjs
    // Set South African locale and timezone
    { provide: LOCALE_ID, useValue: 'en-ZA' },
  ],
};
