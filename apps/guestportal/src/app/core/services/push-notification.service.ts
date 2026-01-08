import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RoomContextService } from './room-context.service';
import { TenantService } from './tenant.service';

export interface PushSubscriptionState {
  isSupported: boolean;
  isSubscribed: boolean;
  permission: NotificationPermission;
  subscription?: PushSubscription;
  roomNumber?: string;
  isVerified?: boolean;
  guestName?: string;
}

export interface SubscriptionResult {
  success: boolean;
  error?: string;
  requiresRoomNumber?: boolean;
  roomNumber?: string;
  isVerified?: boolean;
  guestName?: string;
}

export interface GuestNotificationPayload {
  title: string;
  body: string;
  icon?: string;
  badge?: string;
  data?: any;
  tag?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PushNotificationService {
  private http = inject(HttpClient);
  private roomContext = inject(RoomContextService);
  private tenantService = inject(TenantService);

  private subscriptionState$ = new BehaviorSubject<PushSubscriptionState>({
    isSupported: this.isSupported(),
    isSubscribed: false,
    permission: this.getPermission()
  });

  private badgeCount$ = new BehaviorSubject<number>(0);

  private readonly baseApiUrl = environment.apiUrl;
  private readonly vapidPublicKey = (environment as any).vapidPublicKey || '';

  private get apiUrl(): string {
    const slug = this.tenantService.getTenantSlug();
    return `${this.baseApiUrl}/api/public/${slug}`;
  }

  constructor() {
    this.initialize();
  }

  /**
   * Initialize push notification service
   */
  private async initialize(): Promise<void> {
    if (!this.isSupported()) {
      console.warn('Push notifications not supported');
      return;
    }

    await this.checkSubscriptionStatus();
    this.listenToServiceWorker();
  }

  /**
   * Listen to messages from service worker
   */
  private listenToServiceWorker(): void {
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.addEventListener('message', (event) => {
        if (event.data?.type === 'BADGE_COUNT') {
          this.badgeCount$.next(event.data.count || 0);
        }
        if (event.data?.type === 'NOTIFICATION_CLICK') {
          // Handle notification click navigation
          if (event.data.url) {
            window.location.href = event.data.url;
          }
        }
      });

      // Request initial badge count
      this.requestBadgeCount();
    }
  }

  /**
   * Request badge count from service worker
   */
  private async requestBadgeCount(): Promise<void> {
    try {
      const registration = await navigator.serviceWorker.ready;
      registration.active?.postMessage({ type: 'GET_BADGE' });
    } catch (error) {
      console.error('Error requesting badge count:', error);
    }
  }

  /**
   * Check if push notifications are supported
   */
  isSupported(): boolean {
    return (
      'serviceWorker' in navigator &&
      'PushManager' in window &&
      'Notification' in window
    );
  }

  /**
   * Get current permission status
   */
  getPermission(): NotificationPermission {
    if (!('Notification' in window)) {
      return 'default';
    }
    return Notification.permission;
  }

  /**
   * Get subscription state as observable
   */
  getSubscriptionState(): Observable<PushSubscriptionState> {
    return this.subscriptionState$.asObservable();
  }

  /**
   * Get badge count as observable
   */
  getBadgeCount(): Observable<number> {
    return this.badgeCount$.asObservable();
  }

  /**
   * Request notification permission
   */
  async requestPermission(): Promise<NotificationPermission> {
    if (!('Notification' in window)) {
      console.warn('Notifications not supported');
      return 'denied';
    }

    try {
      const permission = await Notification.requestPermission();
      console.log('Notification permission:', permission);

      this.updateState({ permission });
      return permission;
    } catch (error) {
      console.error('Error requesting notification permission:', error);
      return 'denied';
    }
  }

  /**
   * Subscribe to push notifications (legacy - for backwards compatibility)
   */
  async subscribe(): Promise<boolean> {
    const result = await this.subscribeWithValidation();
    return result.success;
  }

  /**
   * Subscribe to push notifications with phone validation
   * @param phone Guest's phone number (optional - will be retrieved from localStorage)
   * @param roomNumber Room number (optional - will be retrieved from RoomContextService)
   */
  async subscribeWithValidation(phone?: string, roomNumber?: string): Promise<SubscriptionResult> {
    if (!this.isSupported()) {
      console.warn('Push notifications not supported');
      return { success: false, error: 'Push notifications not supported in this browser' };
    }

    // Get phone from localStorage if not provided
    const guestPhone = phone || localStorage.getItem('staybot_guest_phone') || '';

    // Get room number from parameter, service, or localStorage
    const guestRoom = roomNumber || this.roomContext.getRoomNumber() || '';

    // Need at least phone OR room number to identify the guest
    if (!guestPhone && !guestRoom) {
      return {
        success: false,
        error: 'Please enter your room number first to receive notifications',
        requiresRoomNumber: true
      };
    }

    // Request permission first
    const permission = await this.requestPermission();
    if (permission !== 'granted') {
      console.warn('Notification permission not granted');
      return { success: false, error: 'Notification permission not granted' };
    }

    try {
      // Get service worker registration
      const registration = await navigator.serviceWorker.ready;

      // Check if already subscribed
      let subscription = await registration.pushManager.getSubscription();

      if (!subscription) {
        // Convert VAPID key to Uint8Array
        const convertedVapidKey = this.urlBase64ToUint8Array(this.vapidPublicKey);

        // Subscribe
        subscription = await registration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: convertedVapidKey
        });

        console.log('Push subscription created');
      }

      // Send subscription to backend with phone/room identification
      const result = await this.sendSubscriptionToBackendWithValidation(subscription, guestPhone, guestRoom);

      if (result.success) {
        // Store phone for future use if provided
        if (guestPhone) {
          localStorage.setItem('staybot_guest_phone', guestPhone);
        }

        this.updateState({
          isSubscribed: true,
          subscription,
          roomNumber: result.roomNumber,
          isVerified: result.isVerified,
          guestName: result.guestName
        });
      }

      return result;
    } catch (error: any) {
      console.error('Error subscribing to push notifications:', error);
      return {
        success: false,
        error: error?.error || 'Failed to subscribe to notifications'
      };
    }
  }

  /**
   * Get stored guest phone number
   */
  getStoredPhone(): string | null {
    return localStorage.getItem('staybot_guest_phone');
  }

  /**
   * Set guest phone number
   */
  setPhone(phone: string): void {
    localStorage.setItem('staybot_guest_phone', phone);
  }

  /**
   * Unsubscribe from push notifications
   */
  async unsubscribe(): Promise<boolean> {
    if (!this.isSupported()) {
      return false;
    }

    try {
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      if (subscription) {
        await subscription.unsubscribe();
        await this.removeSubscriptionFromBackend(subscription);

        console.log('Unsubscribed from push notifications');

        this.updateState({
          isSubscribed: false,
          subscription: undefined
        });

        return true;
      }

      return false;
    } catch (error) {
      console.error('Error unsubscribing from push notifications:', error);
      return false;
    }
  }

  /**
   * Clear badge count
   */
  async clearBadge(): Promise<void> {
    try {
      const registration = await navigator.serviceWorker.ready;
      registration.active?.postMessage({ type: 'CLEAR_BADGE' });
      this.badgeCount$.next(0);

      // Also clear the app badge
      if ('clearAppBadge' in navigator) {
        await (navigator as any).clearAppBadge();
      }
    } catch (error) {
      console.error('Error clearing badge:', error);
    }
  }

  /**
   * Check current subscription status
   */
  private async checkSubscriptionStatus(): Promise<void> {
    if (!this.isSupported()) {
      return;
    }

    try {
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      this.updateState({
        isSubscribed: subscription !== null,
        subscription: subscription || undefined
      });
    } catch (error) {
      console.error('Error checking subscription status:', error);
    }
  }

  /**
   * Send subscription to backend with phone validation (guest-specific)
   */
  private async sendSubscriptionToBackendWithValidation(
    subscription: PushSubscription,
    phone: string,
    roomNumber?: string
  ): Promise<SubscriptionResult> {
    try {
      const subscriptionData = {
        endpoint: subscription.endpoint,
        keys: {
          p256dh: this.arrayBufferToBase64(subscription.getKey('p256dh')),
          auth: this.arrayBufferToBase64(subscription.getKey('auth'))
        },
        phone,
        roomNumber: roomNumber || this.roomContext.getRoomNumber(),
        deviceInfo: navigator.userAgent
      };

      const response = await this.http.post<SubscriptionResult>(
        `${this.apiUrl}/push/subscribe`,
        subscriptionData
      ).toPromise();

      console.log('Guest subscription sent to backend:', response);
      return response || { success: false, error: 'No response from server' };
    } catch (error: any) {
      console.error('Error sending subscription to backend:', error);

      // Parse error response
      if (error?.error) {
        return {
          success: false,
          error: error.error.error || 'Subscription failed',
          requiresRoomNumber: error.error.requiresRoomNumber
        };
      }

      return { success: false, error: 'Failed to send subscription to server' };
    }
  }

  /**
   * Send subscription to backend (legacy - for backwards compatibility)
   */
  private async sendSubscriptionToBackend(subscription: PushSubscription): Promise<void> {
    const phone = this.getStoredPhone() || '';
    const roomNumber = this.roomContext.getRoomNumber();

    const subscriptionData = {
      endpoint: subscription.endpoint,
      keys: {
        p256dh: this.arrayBufferToBase64(subscription.getKey('p256dh')),
        auth: this.arrayBufferToBase64(subscription.getKey('auth'))
      },
      phone,
      roomNumber,
      deviceInfo: navigator.userAgent
    };

    await this.http.post(`${this.apiUrl}/push/subscribe`, subscriptionData).toPromise();
    console.log('Guest subscription sent to backend');
  }

  /**
   * Remove subscription from backend
   */
  private async removeSubscriptionFromBackend(subscription: PushSubscription): Promise<void> {
    try {
      await this.http.post(`${this.apiUrl}/push/unsubscribe`, {
        endpoint: subscription.endpoint
      }).toPromise();
      console.log('Guest subscription removed from backend');
    } catch (error) {
      console.error('Error removing subscription from backend:', error);
    }
  }

  /**
   * Convert VAPID key from base64 to Uint8Array
   */
  private urlBase64ToUint8Array(base64String: string): Uint8Array {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/\-/g, '+').replace(/_/g, '/');

    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i);
    }

    return outputArray;
  }

  /**
   * Convert ArrayBuffer to base64
   */
  private arrayBufferToBase64(buffer: ArrayBuffer | null): string {
    if (!buffer) {
      return '';
    }

    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return window.btoa(binary);
  }

  /**
   * Update state
   */
  private updateState(state: Partial<PushSubscriptionState>): void {
    this.subscriptionState$.next({
      ...this.subscriptionState$.value,
      ...state
    });
  }

  /**
   * Check if user has seen notification prompt
   */
  hasSeenPrompt(): boolean {
    return localStorage.getItem('staybot_notification_prompt_seen') === 'true';
  }

  /**
   * Mark notification prompt as seen
   */
  markPromptSeen(): void {
    localStorage.setItem('staybot_notification_prompt_seen', 'true');
  }

  /**
   * Should show notification opt-in prompt
   */
  shouldShowPrompt(): boolean {
    return (
      this.isSupported() &&
      this.getPermission() === 'default' &&
      !this.hasSeenPrompt()
    );
  }
}
