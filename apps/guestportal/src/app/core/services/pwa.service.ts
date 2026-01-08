import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface PWAInstallPrompt {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Injectable({
  providedIn: 'root'
})
export class PwaService {
  private deferredPrompt: PWAInstallPrompt | null = null;
  private _installPromptAvailable$ = new BehaviorSubject<boolean>(false);
  private _updateAvailable$ = new BehaviorSubject<boolean>(false);

  public installPromptAvailable$ = this._installPromptAvailable$.asObservable();
  public updateAvailable$ = this._updateAvailable$.asObservable();

  constructor() {
    this.initPWA();
  }

  /**
   * Initialize PWA install prompt listener
   */
  private initPWA(): void {
    if (!this.isPWASupported()) {
      console.log('PWA features not supported in this browser');
      return;
    }

    // Listen for the beforeinstallprompt event
    window.addEventListener('beforeinstallprompt', (event: Event) => {
      event.preventDefault();
      this.deferredPrompt = event as any;
      this._installPromptAvailable$.next(true);
      console.log('PWA install prompt ready');
    });

    // Listen for app installed event
    window.addEventListener('appinstalled', () => {
      console.log('PWA installed successfully');
      this.deferredPrompt = null;
      this._installPromptAvailable$.next(false);
    });
  }

  /**
   * Check if PWA features are supported
   */
  isPWASupported(): boolean {
    return 'serviceWorker' in navigator;
  }

  /**
   * Check if app is already installed
   */
  isInstalled(): boolean {
    // Check if running in standalone mode (installed PWA)
    return window.matchMedia('(display-mode: standalone)').matches ||
           (window.navigator as any).standalone === true;
  }

  /**
   * Prompt user to install the app
   */
  async promptInstall(): Promise<boolean> {
    if (!this.deferredPrompt) {
      console.log('No install prompt available');
      return false;
    }

    try {
      await this.deferredPrompt.prompt();
      const { outcome } = await this.deferredPrompt.userChoice;

      console.log(`User ${outcome} the install prompt`);

      if (outcome === 'accepted') {
        this.deferredPrompt = null;
        this._installPromptAvailable$.next(false);
        return true;
      }

      return false;
    } catch (error) {
      console.error('Error showing install prompt:', error);
      return false;
    }
  }

  /**
   * Get installation status
   */
  getInstallStatus(): {
    isSupported: boolean;
    isInstalled: boolean;
    canPrompt: boolean;
  } {
    return {
      isSupported: this.isPWASupported(),
      isInstalled: this.isInstalled(),
      canPrompt: this.deferredPrompt !== null
    };
  }

  /**
   * Dismiss update notification
   */
  dismissUpdateNotification(): void {
    this._updateAvailable$.next(false);
  }
}
