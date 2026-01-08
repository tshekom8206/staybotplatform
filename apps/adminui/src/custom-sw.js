/**
 * Custom Service Worker for StayBOT Admin Portal
 * Handles push notifications and displays native OS notifications
 */

// Listen for push events from the server
self.addEventListener('push', (event) => {
  console.log('[SW] Push event received');

  let data = {
    title: 'StayBOT Notification',
    body: 'You have a new notification',
    icon: '/icons/icon-192x192.png',
    badge: '/icons/icon-72x72.png'
  };

  try {
    if (event.data) {
      data = event.data.json();
    }
  } catch (e) {
    console.error('[SW] Error parsing push data:', e);
  }

  const options = {
    body: data.body,
    icon: data.icon || '/icons/icon-192x192.png',
    badge: data.badge || '/icons/icon-72x72.png',
    tag: data.tag || 'staybot-notification',
    data: data.data || {},
    requireInteraction: data.requireInteraction || false,
    vibrate: data.vibrate || [200, 100, 200],
    actions: data.actions || [],
    silent: data.silent || false
  };

  // Add image if provided
  if (data.image) {
    options.image = data.image;
  }

  event.waitUntil(
    self.registration.showNotification(data.title, options)
  );
});

// Handle notification click
self.addEventListener('notificationclick', (event) => {
  console.log('[SW] Notification clicked');

  event.notification.close();

  const notificationData = event.notification.data || {};
  const urlToOpen = notificationData.url || '/';

  // Handle action button clicks
  if (event.action) {
    console.log('[SW] Action clicked:', event.action);
    // Handle specific actions if needed
  }

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true })
      .then((windowClients) => {
        // Check if app is already open
        for (const client of windowClients) {
          if (client.url.includes(self.location.origin) && 'focus' in client) {
            // Navigate existing window to the URL
            client.postMessage({
              type: 'NOTIFICATION_CLICK',
              url: urlToOpen,
              data: notificationData
            });
            return client.focus();
          }
        }
        // Open new window if app not already open
        if (clients.openWindow) {
          return clients.openWindow(urlToOpen);
        }
      })
  );
});

// Handle notification close
self.addEventListener('notificationclose', (event) => {
  console.log('[SW] Notification closed');
  // Can track analytics here if needed
});

// Listen for messages from the main app
self.addEventListener('message', (event) => {
  console.log('[SW] Message received:', event.data);

  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});

// Service worker activation
self.addEventListener('activate', (event) => {
  console.log('[SW] Activated');
  event.waitUntil(self.clients.claim());
});

// Service worker installation
self.addEventListener('install', (event) => {
  console.log('[SW] Installed');
  self.skipWaiting();
});
