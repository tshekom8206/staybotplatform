import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgbDropdownModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { Subject, takeUntil } from 'rxjs';
import { CommonModule } from '@angular/common';
import { ThemeModeService } from '../../../core/services/theme-mode.service';
import { AuthService } from '../../../core/services/auth.service';
import { SignalRService, ConnectionState } from '../../../core/services/signalr.service';
import { SoundService, SoundSettings } from '../../../core/services/sound.service';
import { NotificationService, Notification } from '../../../core/services/notification.service';
import { User, Tenant } from '../../../core/models/user.model';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [
    NgbDropdownModule,
    NgbTooltipModule,
    RouterLink,
    CommonModule
  ],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.scss'
})
export class NavbarComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  currentTheme: string;
  currentUser: User | null = null;
  currentTenant: Tenant | null = null;
  connectionState: ConnectionState = ConnectionState.Disconnected;
  unreadNotifications = 0;
  notifications: Notification[] = [];
  soundSettings: SoundSettings = {
    enabled: false,
    volume: 0.7,
    taskCreationSound: true,
    taskCompletionSound: false,
    emergencySound: true
  };

  constructor(
    private router: Router,
    private themeModeService: ThemeModeService,
    private authService: AuthService,
    private signalRService: SignalRService,
    public soundService: SoundService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    // Theme subscription
    this.themeModeService.currentTheme.subscribe( (theme) => {
      this.currentTheme = theme;
      this.showActiveTheme(this.currentTheme);
    });

    // User subscription
    this.authService.currentUser
      .pipe(takeUntil(this.destroy$))
      .subscribe(user => {
        this.currentUser = user;
      });

    // Tenant subscription
    this.authService.currentTenant
      .pipe(takeUntil(this.destroy$))
      .subscribe(tenant => {
        this.currentTenant = tenant;
      });

    // SignalR connection state
    this.signalRService.connectionState$
      .pipe(takeUntil(this.destroy$))
      .subscribe(state => {
        this.connectionState = state;
      });

    // Load notifications
    this.notificationService.getNotifications().subscribe();

    // Subscribe to notifications and update both the list and count
    this.notificationService.notifications$
      .pipe(takeUntil(this.destroy$))
      .subscribe(notifications => {
        this.notifications = notifications;
        this.unreadNotifications = notifications.filter(n => !n.read).length;
      });

    // Sound settings subscription
    this.soundService.settings$
      .pipe(takeUntil(this.destroy$))
      .subscribe(settings => {
        this.soundSettings = settings;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  showActiveTheme(theme: string) {
    const themeSwitcher = document.querySelector('#theme-switcher') as HTMLInputElement;
    const box = document.querySelector('.box') as HTMLElement;

    if (!themeSwitcher) {
      return;
    }

    // Toggle the custom checkbox based on the theme
    if (theme === 'dark') {
      themeSwitcher.checked = true;
      box.classList.remove('light');
      box.classList.add('dark');
    } else if (theme === 'light') {
      themeSwitcher.checked = false;
      box.classList.remove('dark');
      box.classList.add('light');
    }
  }

  /**
   * Change the theme on #theme-switcher checkbox changes 
   */
  onThemeCheckboxChange(e: Event) {
    const checkbox = e.target as HTMLInputElement;
    const newTheme: string = checkbox.checked ? 'dark' : 'light';
    this.themeModeService.toggleTheme(newTheme);
    this.showActiveTheme(newTheme);
  }

  /**
   * Toggle the sidebar when the hamburger button is clicked
   */
  toggleSidebar(e: Event) {
    e.preventDefault();
    document.body.classList.add('sidebar-open');
    document.querySelector('.sidebar .sidebar-toggler')?.classList.add('active');
  }

  /**
   * Logout
   */
  onLogout(e: Event) {
    e.preventDefault();

    // Stop SignalR connection
    this.signalRService.stopConnection();

    // Use auth service logout
    this.authService.logout();
  }

  /**
   * Get connection status text
   */
  getConnectionStatusText(): string {
    switch (this.connectionState) {
      case ConnectionState.Connected: return 'Online';
      case ConnectionState.Connecting: return 'Connecting...';
      case ConnectionState.Reconnecting: return 'Reconnecting...';
      case ConnectionState.Disconnected: return 'Offline';
      case ConnectionState.Failed: return 'Connection Failed';
      default: return 'Unknown';
    }
  }

  /**
   * Get connection status class
   */
  getConnectionStatusClass(): string {
    switch (this.connectionState) {
      case ConnectionState.Connected: return 'text-success';
      case ConnectionState.Connecting:
      case ConnectionState.Reconnecting: return 'text-warning';
      case ConnectionState.Disconnected:
      case ConnectionState.Failed: return 'text-danger';
      default: return 'text-secondary';
    }
  }

  /**
   * Clear notifications
   */
  clearNotifications(): void {
    this.unreadNotifications = 0;
  }

  /**
   * Navigate to edit profile
   */
  onEditProfile(e: Event): void {
    e.preventDefault();
    this.router.navigate(['/general/profile'], {
      queryParams: { openEditModal: 'true' }
    });
  }

  /**
   * Load notifications from API
   */
  private loadNotifications(): void {
    this.notificationService.getNotifications()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (notifications) => {
          this.notifications = notifications;
        },
        error: (error) => {
          console.error('Error loading notifications:', error);
          // Fallback to empty array on error
          this.notifications = [];
        }
      });
  }

  /**
   * Clear all notifications
   */
  clearAllNotifications(): void {
    this.notificationService.markAllAsRead();
  }

  /**
   * Get background color class for notification icon
   */
  getNotificationIconClass(notification: Notification): string {
    switch (notification.iconColor) {
      case 'success': return 'bg-success';
      case 'primary': return 'bg-primary';
      case 'warning': return 'bg-warning';
      case 'danger': return 'bg-danger';
      default: return 'bg-primary';
    }
  }

  /**
   * Format time ago for notifications
   */
  formatTimeAgo(date: Date): string {
    return this.notificationService.formatTimeAgo(date);
  }

  /**
   * Toggle sound notifications on/off
   */
  toggleSound(): void {
    this.soundService.toggleSound();
  }

  /**
   * Test sound playback
   */
  testSound(): void {
    this.soundService.testSound();
  }

  /**
   * Check if sounds are supported
   */
  isSoundSupported(): boolean {
    return this.soundService.isSupported();
  }

  /**
   * Handle notification click - mark as read and navigate
   */
  onNotificationClick(notification: Notification, event?: Event): void {
    // Mark as read
    this.notificationService.markAsRead(notification.id);

    // Navigate to the action URL if it exists
    if (notification.actionUrl) {
      this.router.navigate([notification.actionUrl]);
    }
  }

  /**
   * View all notifications - navigate to notifications page
   */
  viewAllNotifications(event: Event): void {
    event.preventDefault();
    this.router.navigate(['/tasks/my']);
  }

}
