/**
 * Custom Service Worker for StayBOT Guest Portal (PWA)
 * Handles push notifications with badge support and quiet hours
 */

// Quiet hours configuration (10pm - 8am)
const QUIET_HOURS_START = 22; // 10 PM
const QUIET_HOURS_END = 8;    // 8 AM

// Badge count stored in IndexedDB
let unreadCount = 0;

// Check if current time is within quiet hours
function isQuietHours() {
  const now = new Date();
  const hour = now.getHours();
  return hour >= QUIET_HOURS_START || hour < QUIET_HOURS_END;
}

// Update badge count
async function updateBadge(count) {
  unreadCount = count;
  if ('setAppBadge' in navigator) {
    try {
      if (count > 0) {
        await navigator.setAppBadge(count);
      } else {
        await navigator.clearAppBadge();
      }
    } catch (error) {
      console.error('[SW] Error updating badge:', error);
    }
  }
}

// Increment badge count
async function incrementBadge() {
  await updateBadge(unreadCount + 1);
}

// Clear badge
async function clearBadge() {
  await updateBadge(0);
}

// Listen for push events from the server
self.addEventListener('push', (event) => {
  console.log('[SW] Push event received');

  let data = {
    title: 'StayBOT',
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

  // Check notification type for quiet hours exception
  const isEmergency = data.data?.type === 'emergency';
  const inQuietHours = isQuietHours();

  // Don't show notifications during quiet hours (except emergencies)
  if (inQuietHours && !isEmergency) {
    console.log('[SW] Quiet hours - notification suppressed (badge updated)');
    // Still update badge count
    event.waitUntil(incrementBadge());
    return;
  }

  const options = {
    body: data.body,
    icon: data.icon || '/icons/icon-192x192.png',
    badge: data.badge || '/icons/icon-72x72.png',
    tag: data.tag || 'staybot-guest-notification',
    data: data.data || {},
    requireInteraction: data.requireInteraction || false,
    vibrate: isEmergency ? [300, 100, 300, 100, 300] : [200, 100, 200],
    silent: inQuietHours && !isEmergency,
    actions: data.actions || []
  };

  // Add image if provided
  if (data.image) {
    options.image = data.image;
  }

  event.waitUntil(
    Promise.all([
      self.registration.showNotification(data.title, options),
      incrementBadge()
    ])
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
  }

  event.waitUntil(
    Promise.all([
      // Clear or decrement badge when notification is clicked
      updateBadge(Math.max(0, unreadCount - 1)),
      // Open or focus the app
      clients.matchAll({ type: 'window', includeUncontrolled: true })
        .then((windowClients) => {
          // Check if app is already open
          for (const client of windowClients) {
            if (client.url.includes(self.location.origin) && 'focus' in client) {
              // Send message to navigate
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
    ])
  );
});

// Handle notification close (dismissed without clicking)
self.addEventListener('notificationclose', (event) => {
  console.log('[SW] Notification closed');
});

// Listen for messages from the main app
self.addEventListener('message', (event) => {
  console.log('[SW] Message received:', event.data);

  if (event.data) {
    switch (event.data.type) {
      case 'SKIP_WAITING':
        self.skipWaiting();
        break;
      case 'CLEAR_BADGE':
        clearBadge();
        break;
      case 'SET_BADGE':
        updateBadge(event.data.count || 0);
        break;
      case 'GET_BADGE':
        event.source?.postMessage({ type: 'BADGE_COUNT', count: unreadCount });
        break;
    }
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
