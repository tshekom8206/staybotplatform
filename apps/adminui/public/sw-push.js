// Custom Service Worker for Push Notifications
// This handles push notifications and forwards them to the Angular app

self.addEventListener('push', function(event) {
  console.log('[Service Worker] Push Received');

  let data = {};

  if (event.data) {
    try {
      data = event.data.json();
      console.log('[Service Worker] Push data:', data);
    } catch (e) {
      console.log('[Service Worker] Push data (text):', event.data.text());
      data = {
        title: 'Notification',
        body: event.data.text()
      };
    }
  }

  const title = data.title || 'Hostr Notification';
  const options = {
    body: data.body || 'You have a new notification',
    icon: data.icon || '/icons/icon-192x192.png',
    badge: data.badge || '/icons/badge-72x72.png',
    tag: data.tag || 'default',
    requireInteraction: data.requireInteraction || false,
    data: data.data || {},
    vibrate: [200, 100, 200],
    actions: data.actions || []
  };

  // Show the notification
  const notificationPromise = self.registration.showNotification(title, options);

  // Forward the push notification to all clients (active tabs)
  const clientsPromise = self.clients.matchAll({
    includeUncontrolled: true,
    type: 'window'
  }).then(clients => {
    clients.forEach(client => {
      client.postMessage({
        type: 'PUSH_NOTIFICATION',
        payload: data
      });
    });
  });

  event.waitUntil(Promise.all([notificationPromise, clientsPromise]));
});

self.addEventListener('notificationclick', function(event) {
  console.log('[Service Worker] Notification click received:', event.notification);

  event.notification.close();

  // Get the URL to navigate to
  const urlToOpen = event.notification.data?.url || '/dashboard';

  event.waitUntil(
    self.clients.matchAll({
      type: 'window',
      includeUncontrolled: true
    }).then(function(clientList) {
      // Check if there's already a window/tab open
      for (let i = 0; i < clientList.length; i++) {
        const client = clientList[i];
        if (client.url.includes(self.location.origin) && 'focus' in client) {
          // Focus existing window and navigate
          return client.focus().then(client => {
            if ('navigate' in client) {
              return client.navigate(urlToOpen);
            }
          });
        }
      }
      // No window open, open a new one
      if (self.clients.openWindow) {
        return self.clients.openWindow(urlToOpen);
      }
    })
  );
});

// Handle service worker activation
self.addEventListener('activate', event => {
  console.log('[Service Worker] Activating...');
  event.waitUntil(self.clients.claim());
});

// Handle service worker installation
self.addEventListener('install', event => {
  console.log('[Service Worker] Installing...');
  self.skipWaiting();
});
