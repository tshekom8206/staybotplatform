import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PushNotificationService } from './push-notification.service';
import { SoundService } from './sound.service';

export interface Notification {
  id: string;
  type: 'task' | 'emergency' | 'maintenance' | 'order' | 'customer' | 'system';
  title: string;
  message: string;
  priority: 'low' | 'normal' | 'high' | 'urgent' | 'critical';
  icon: string;
  iconColor: string;
  timestamp: Date;
  read: boolean;
  actionUrl?: string;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private apiUrl = environment.apiUrl;
  private notificationsSubject = new BehaviorSubject<Notification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();
  private browserNotificationsEnabled = false;

  constructor(
    private http: HttpClient,
    private pushNotificationService: PushNotificationService,
    private soundService: SoundService
  ) {
    this.initializeBrowserNotifications();
    this.listenToPushNotifications();
  }

  getNotifications(): Observable<Notification[]> {
    // Get recent tasks and convert them to notifications
    return this.http.get<any[]>(`${this.apiUrl}/tasks`).pipe(
      map(tasks => {
        const notifications: Notification[] = [];

        // Only add task notifications if there are recent tasks (created within last 24 hours)
        const twentyFourHoursAgo = new Date(Date.now() - 24 * 60 * 60 * 1000);
        const recentTasks = tasks
          .filter(task => {
            const taskDate = new Date(task.createdAt);
            return task.status === 'Pending' && taskDate > twentyFourHoursAgo;
          })
          .slice(0, 6) // Show up to 6 most recent tasks
          .map(task => {
            const notificationId = `task-${task.id}`;
            return {
              id: notificationId,
              type: 'task' as const,
              title: 'New Task Created',
              message: task.title || task.description || 'A new task has been created',
              priority: this.mapPriority(task.priority),
              icon: 'check-square',
              iconColor: 'success',
              timestamp: new Date(task.createdAt),
              read: false, // Will be loaded from API
              actionUrl: `/tasks/my`
            };
          });

        notifications.push(...recentTasks);

        // Sort by timestamp (newest first)
        notifications.sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime());

        // Load read state from API
        this.loadReadStatesFromApi(notifications);

        return notifications;
      }),
      catchError(error => {
        console.warn('Failed to load notifications from API, falling back to empty array:', error);
        // Return empty array instead of throwing error to prevent UI breakage
        const emptyNotifications: Notification[] = [];
        this.notificationsSubject.next(emptyNotifications);
        return of(emptyNotifications);
      })
    );
  }

  getUnreadCount(): Observable<number> {
    return this.notifications$.pipe(
      map(notifications => notifications.filter(n => !n.read).length)
    );
  }

  markAsRead(notificationId: string): void {
    const notifications = this.notificationsSubject.value;
    const notification = notifications.find(n => n.id === notificationId);
    if (notification) {
      notification.read = true;
      this.notificationsSubject.next([...notifications]);
      this.saveReadStateToApi(notificationId);
    }
  }

  markAllAsRead(): void {
    const notifications = this.notificationsSubject.value;
    const notificationIds = notifications.map(n => n.id);

    notifications.forEach(n => {
      n.read = true;
    });
    this.notificationsSubject.next([...notifications]);

    this.saveAllReadStatesToApi(notificationIds);
  }

  /**
   * Load read states from API for given notifications
   */
  private loadReadStatesFromApi(notifications: Notification[]): void {
    this.http.get<any[]>(`${this.apiUrl}/notification/read`).subscribe({
      next: (readNotifications) => {
        const readIds = new Set(readNotifications.map((n: any) => n.notificationId));

        notifications.forEach(notification => {
          notification.read = readIds.has(notification.id);
        });

        this.notificationsSubject.next(notifications);
      },
      error: (error) => {
        console.warn('Failed to load notification read states from API:', error);
        // Keep notifications as unread if API call fails
        this.notificationsSubject.next(notifications);
      }
    });
  }

  /**
   * Save notification read state to API
   */
  private saveReadStateToApi(notificationId: string): void {
    this.http.post(`${this.apiUrl}/notification/mark-read`, { notificationId }).subscribe({
      next: () => {
        console.log('Notification marked as read:', notificationId);
      },
      error: (error) => {
        console.warn('Failed to save notification read state to API:', error);
      }
    });
  }

  /**
   * Save all notification read states to API
   */
  private saveAllReadStatesToApi(notificationIds: string[]): void {
    this.http.post(`${this.apiUrl}/notification/mark-all-read`, { notificationIds }).subscribe({
      next: () => {
        console.log('All notifications marked as read');
      },
      error: (error) => {
        console.warn('Failed to save all notification read states to API:', error);
      }
    });
  }

  private mapPriority(priority: string): 'low' | 'normal' | 'high' | 'urgent' | 'critical' {
    switch (priority?.toLowerCase()) {
      case 'low': return 'low';
      case 'high': return 'high';
      case 'urgent': return 'urgent';
      case 'critical': return 'critical';
      default: return 'normal';
    }
  }

  formatTimeAgo(date: Date): string {
    const now = new Date();
    const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffInSeconds < 60) {
      return `${diffInSeconds} sec ago`;
    } else if (diffInSeconds < 3600) {
      const minutes = Math.floor(diffInSeconds / 60);
      return `${minutes} min ago`;
    } else if (diffInSeconds < 86400) {
      const hours = Math.floor(diffInSeconds / 3600);
      return `${hours} hrs ago`;
    } else {
      const days = Math.floor(diffInSeconds / 86400);
      return `${days} days ago`;
    }
  }

  /**
   * Initialize browser notifications
   */
  private async initializeBrowserNotifications(): Promise<void> {
    const state = await this.pushNotificationService.getSubscriptionState().toPromise();

    if (state && state.isSupported && state.permission === 'granted') {
      this.browserNotificationsEnabled = true;
      console.log('Browser notifications enabled');
    }
  }

  /**
   * Show browser notification for a notification item
   */
  async showBrowserNotification(notification: Notification): Promise<void> {
    if (!this.browserNotificationsEnabled) {
      return;
    }

    await this.pushNotificationService.showLocalNotification({
      title: notification.title,
      body: notification.message,
      icon: this.getIconPath(notification.icon),
      tag: notification.id,
      requireInteraction: notification.priority === 'urgent' || notification.priority === 'critical',
      data: {
        url: notification.actionUrl,
        notificationId: notification.id
      }
    });
  }

  /**
   * Request permission and enable browser notifications
   */
  async enableBrowserNotifications(): Promise<boolean> {
    const permission = await this.pushNotificationService.requestPermission();

    if (permission === 'granted') {
      this.browserNotificationsEnabled = true;
      return true;
    }

    return false;
  }

  /**
   * Disable browser notifications
   */
  disableBrowserNotifications(): void {
    this.browserNotificationsEnabled = false;
  }

  /**
   * Check if browser notifications are enabled
   */
  areBrowserNotificationsEnabled(): boolean {
    return this.browserNotificationsEnabled;
  }

  /**
   * Get icon path for notification
   */
  private getIconPath(icon: string): string {
    // Map feather icon names to actual icon paths
    // For now, use the PWA icon
    return '/icons/icon-192x192.png';
  }

  /**
   * Add new notification and show browser notification if enabled
   */
  async addNotification(notification: Notification): Promise<void> {
    const notifications = this.notificationsSubject.value;
    notifications.unshift(notification);
    this.notificationsSubject.next(notifications);

    // Play notification sound based on type
    await this.playNotificationSound(notification);

    // Show browser notification if enabled
    if (this.browserNotificationsEnabled) {
      await this.showBrowserNotification(notification);
    }
  }

  /**
   * Play appropriate sound for notification type
   */
  private async playNotificationSound(notification: Notification): Promise<void> {
    try {
      if (notification.type === 'emergency') {
        await this.soundService.playEmergencyAlert();
      } else if (notification.type === 'task' || notification.type === 'customer') {
        await this.soundService.playServiceBell();
      }
    } catch (error) {
      console.warn('Failed to play notification sound:', error);
    }
  }

  /**
   * Show notification for new task
   */
  async notifyNewTask(task: any): Promise<void> {
    const notification: Notification = {
      id: `task-${task.id}`,
      type: 'task',
      title: 'New Task Assigned',
      message: task.title || task.description || 'A new task has been assigned to you',
      priority: this.mapPriority(task.priority),
      icon: 'check-square',
      iconColor: 'success',
      timestamp: new Date(),
      read: false,
      actionUrl: `/tasks/my`
    };

    await this.addNotification(notification);
  }

  /**
   * Show notification for new emergency
   */
  async notifyEmergency(emergency: any): Promise<void> {
    const notification: Notification = {
      id: `emergency-${emergency.id}`,
      type: 'emergency',
      title: 'Emergency Alert',
      message: emergency.description || 'New emergency incident reported',
      priority: 'critical',
      icon: 'alert-triangle',
      iconColor: 'danger',
      timestamp: new Date(),
      read: false,
      actionUrl: `/emergencies/${emergency.id}`
    };

    await this.addNotification(notification);
  }

  /**
   * Show notification for new conversation
   */
  async notifyNewConversation(conversation: any): Promise<void> {
    const notification: Notification = {
      id: `conversation-${conversation.id}`,
      type: 'customer',
      title: 'New Conversation',
      message: `New conversation from ${conversation.guestPhone || 'guest'}`,
      priority: 'high',
      icon: 'message-square',
      iconColor: 'primary',
      timestamp: new Date(),
      read: false,
      actionUrl: `/conversations/${conversation.id}`
    };

    await this.addNotification(notification);
  }

  /**
   * Listen to push notifications from service worker
   */
  private listenToPushNotifications(): void {
    if (!('serviceWorker' in navigator)) {
      return;
    }

    // Listen for messages from service worker
    navigator.serviceWorker.addEventListener('message', async (event) => {
      console.log('Received push notification message:', event.data);

      if (event.data && event.data.type === 'PUSH_NOTIFICATION') {
        const payload = event.data.payload;
        await this.handlePushNotification(payload);
      }
    });
  }

  /**
   * Handle incoming push notification
   */
  private async handlePushNotification(payload: any): Promise<void> {
    console.log('Handling push notification:', payload);

    // Determine notification type and create appropriate notification
    let notification: Notification;

    if (payload.data?.type === 'conversation_assigned') {
      // Conversation assignment notification
      notification = {
        id: `conversation-${payload.data.conversationId}-${Date.now()}`,
        type: 'customer',
        title: payload.title || 'New Conversation Assigned',
        message: payload.body || 'You have been assigned a new conversation',
        priority: 'high',
        icon: 'message-circle',
        iconColor: 'primary',
        timestamp: new Date(),
        read: false,
        actionUrl: `/conversations/active?conversation=${payload.data.conversationId}`
      };
    } else if (payload.data?.type === 'task_assigned') {
      // Task assignment notification
      notification = {
        id: `task-${payload.data.taskId}-${Date.now()}`,
        type: 'task',
        title: payload.title || 'New Task Assigned',
        message: payload.body || 'You have been assigned a new task',
        priority: this.mapPriority(payload.data.priority || 'normal'),
        icon: 'check-square',
        iconColor: 'success',
        timestamp: new Date(),
        read: false,
        actionUrl: `/tasks/my-tasks?task=${payload.data.taskId}`
      };
    } else if (payload.data?.type === 'emergency') {
      // Emergency notification
      notification = {
        id: `emergency-${payload.data.emergencyId || Date.now()}`,
        type: 'emergency',
        title: payload.title || 'Emergency Alert',
        message: payload.body || 'Emergency assistance required',
        priority: 'critical',
        icon: 'alert-triangle',
        iconColor: 'danger',
        timestamp: new Date(),
        read: false,
        actionUrl: `/emergencies`
      };
    } else {
      // Generic notification
      notification = {
        id: `notification-${Date.now()}`,
        type: 'system',
        title: payload.title || 'Notification',
        message: payload.body || '',
        priority: 'normal',
        icon: 'bell',
        iconColor: 'primary',
        timestamp: new Date(),
        read: false,
        actionUrl: payload.data?.url || '/dashboard'
      };
    }

    // Add notification to the list (this will update the bell icon)
    await this.addNotification(notification);
  }
}