import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { StorageService } from './storage.service';

export interface InstallPromptState {
  canInstall: boolean;
  isInstalled: boolean;
  isPromptShown: boolean;
  platform?: 'ios' | 'android' | 'desktop' | 'unknown';
}

// Extend Window interface for BeforeInstallPromptEvent
declare global {
  interface Window {
    deferredPrompt?: BeforeInstallPromptEvent;
  }

  interface BeforeInstallPromptEvent extends Event {
    readonly platforms: string[];
    readonly userChoice: Promise<{
      outcome: 'accepted' | 'dismissed';
      platform: string;
    }>;
    prompt(): Promise<void>;
  }
}

@Injectable({
  providedIn: 'root'
})
export class PwaInstallService {
  private installPromptState$ = new BehaviorSubject<InstallPromptState>({
    canInstall: false,
    isInstalled: this.checkIfInstalled(),
    isPromptShown: false,
    platform: this.detectPlatform()
  });

  private deferredPrompt: BeforeInstallPromptEvent | null = null;
  private readonly PROMPT_DISMISSED_KEY = 'pwa_install_prompt_dismissed';
  private readonly PROMPT_DISMISSED_EXPIRY_DAYS = 7;

  constructor(private storageService: StorageService) {
    this.initialize();
  }

  /**
   * Initialize PWA install service
   */
  private initialize(): void {
    // Listen for beforeinstallprompt event
    window.addEventListener('beforeinstallprompt', (e: Event) => {
      e.preventDefault();
      this.deferredPrompt = e as BeforeInstallPromptEvent;

      console.log('PWA install prompt available');

      // Check if user has dismissed the prompt recently
      if (!this.isPromptRecentlyDismissed()) {
        this.updateState({
          canInstall: true,
          isPromptShown: false
        });
      }
    });

    // Listen for app installed event
    window.addEventListener('appinstalled', () => {
      console.log('PWA installed');
      this.deferredPrompt = null;

      this.updateState({
        canInstall: false,
        isInstalled: true,
        isPromptShown: false
      });
    });

    // Check if already installed
    this.checkInstallStatus();
  }

  /**
   * Get install prompt state as observable
   */
  getInstallPromptState(): Observable<InstallPromptState> {
    return this.installPromptState$.asObservable();
  }

  /**
   * Show install prompt
   */
  async showInstallPrompt(): Promise<boolean> {
    if (!this.deferredPrompt) {
      console.warn('Install prompt not available');
      return false;
    }

    try {
      this.updateState({
        ...this.installPromptState$.value,
        isPromptShown: true
      });

      // Show the install prompt
      await this.deferredPrompt.prompt();

      // Wait for the user's response
      const choiceResult = await this.deferredPrompt.userChoice;

      console.log('User choice:', choiceResult.outcome);

      if (choiceResult.outcome === 'accepted') {
        console.log('User accepted the install prompt');
        this.deferredPrompt = null;

        this.updateState({
          canInstall: false,
          isInstalled: true,
          isPromptShown: false
        });

        return true;
      } else {
        console.log('User dismissed the install prompt');
        this.markPromptAsDismissed();

        this.updateState({
          canInstall: true,
          isPromptShown: false
        });

        return false;
      }
    } catch (error) {
      console.error('Error showing install prompt:', error);
      this.updateState({
        ...this.installPromptState$.value,
        isPromptShown: false
      });
      return false;
    }
  }

  /**
   * Dismiss install prompt
   */
  dismissPrompt(): void {
    this.markPromptAsDismissed();

    this.updateState({
      ...this.installPromptState$.value,
      canInstall: false
    });
  }

  /**
   * Check if app is installed
   */
  private checkIfInstalled(): boolean {
    // Check if running in standalone mode
    const isStandalone = window.matchMedia('(display-mode: standalone)').matches;

    // Check for iOS standalone
    const isIosStandalone = (window.navigator as any).standalone === true;

    return isStandalone || isIosStandalone;
  }

  /**
   * Check install status periodically
   */
  private checkInstallStatus(): void {
    const isInstalled = this.checkIfInstalled();

    if (isInstalled) {
      this.updateState({
        canInstall: false,
        isInstalled: true,
        isPromptShown: false
      });
    }
  }

  /**
   * Detect platform
   */
  private detectPlatform(): 'ios' | 'android' | 'desktop' | 'unknown' {
    const userAgent = navigator.userAgent.toLowerCase();

    if (/iphone|ipad|ipod/.test(userAgent)) {
      return 'ios';
    } else if (/android/.test(userAgent)) {
      return 'android';
    } else if (/win|mac|linux/.test(userAgent)) {
      return 'desktop';
    }

    return 'unknown';
  }

  /**
   * Check if prompt was recently dismissed
   */
  private isPromptRecentlyDismissed(): boolean {
    const dismissed = this.storageService.getItem<{ timestamp: number }>(
      this.PROMPT_DISMISSED_KEY
    );

    if (!dismissed) {
      return false;
    }

    const expiryTime = dismissed.timestamp + (this.PROMPT_DISMISSED_EXPIRY_DAYS * 24 * 60 * 60 * 1000);
    const now = Date.now();

    return now < expiryTime;
  }

  /**
   * Mark prompt as dismissed
   */
  private markPromptAsDismissed(): void {
    this.storageService.setItem(this.PROMPT_DISMISSED_KEY, {
      timestamp: Date.now()
    });
  }

  /**
   * Update state
   */
  private updateState(state: Partial<InstallPromptState>): void {
    this.installPromptState$.next({
      ...this.installPromptState$.value,
      ...state
    });
  }

  /**
   * Get iOS install instructions
   */
  getIosInstallInstructions(): string[] {
    return [
      'Tap the Share button in Safari',
      'Scroll down and tap "Add to Home Screen"',
      'Tap "Add" to confirm'
    ];
  }

  /**
   * Check if platform supports install prompt
   */
  canShowInstallPrompt(): boolean {
    const state = this.installPromptState$.value;
    return state.canInstall && !state.isInstalled && this.deferredPrompt !== null;
  }

  /**
   * Check if running as installed PWA
   */
  isRunningAsApp(): boolean {
    return this.installPromptState$.value.isInstalled;
  }

  /**
   * Get current platform
   */
  getPlatform(): 'ios' | 'android' | 'desktop' | 'unknown' {
    return this.installPromptState$.value.platform || 'unknown';
  }

  /**
   * Reset dismissed state (for testing)
   */
  resetDismissedState(): void {
    this.storageService.removeItem(this.PROMPT_DISMISSED_KEY);
    this.checkInstallStatus();
  }
}
