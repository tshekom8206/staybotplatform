/// <reference types="@angular/localize" />

import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));

// Register custom service worker for push notifications
if ('serviceWorker' in navigator) {
  window.addEventListener('load', async () => {
    try {
      // Register custom service worker for push notifications
      const registration = await navigator.serviceWorker.register('/custom-sw.js', {
        scope: '/'
      });
      console.log('Custom SW registered:', registration.scope);

      // Listen for messages from service worker
      navigator.serviceWorker.addEventListener('message', (event) => {
        if (event.data?.type === 'NOTIFICATION_CLICK') {
          // Navigate to the URL from notification
          if (event.data.url) {
            window.location.href = event.data.url;
          }
        }
      });
    } catch (error) {
      console.error('Custom SW registration failed:', error);
    }
  });
}
