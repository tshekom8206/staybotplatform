import { Injectable, ApplicationRef } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter, map, first, concat, interval } from 'rxjs';
import { BehaviorSubject } from 'rxjs';

interface VersionInfo {
  current: { hash: string; appData?: object | null };
  available: { hash: string; appData?: object | null };
}

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

  constructor(
    private swUpdate: SwUpdate,
    private appRef: ApplicationRef
  ) {
    this.initPWA();
    this.checkForUpdates();
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
    return 'serviceWorker' in navigator && 'BeforeInstallPromptEvent' in window;
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
   * Check for app updates periodically
   */
  private checkForUpdates(): void {
    if (!this.swUpdate.isEnabled) {
      console.log('Service Worker updates not enabled');
      return;
    }

    // Check for updates when app becomes stable
    const appIsStable$ = this.appRef.isStable.pipe(
      first(isStable => isStable === true)
    );

    // Check every 6 hours
    const everySixHours$ = interval(6 * 60 * 60 * 1000);
    const everySixHoursOnceAppIsStable$ = concat(appIsStable$, everySixHours$);

    everySixHoursOnceAppIsStable$.subscribe(() => {
      this.swUpdate.checkForUpdate().then(updateFound => {
        if (updateFound) {
          console.log('Update check found new version');
        }
      }).catch(err => {
        console.error('Error checking for updates:', err);
      });
    });

    // Listen for version updates
    this.swUpdate.versionUpdates
      .pipe(
        filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'),
        map((evt: VersionReadyEvent) => ({
          current: evt.currentVersion,
          available: evt.latestVersion
        }))
      )
      .subscribe((update: VersionInfo) => {
        console.log('New version available:', update.available);
        this._updateAvailable$.next(true);
      });
  }

  /**
   * Activate the latest app version
   */
  async activateUpdate(): Promise<void> {
    if (!this.swUpdate.isEnabled) {
      return;
    }

    try {
      await this.swUpdate.activateUpdate();
      this._updateAvailable$.next(false);
      // Reload the page to apply updates
      document.location.reload();
    } catch (error) {
      console.error('Error activating update:', error);
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
   * Clear update notification
   */
  dismissUpdateNotification(): void {
    this._updateAvailable$.next(false);
  }
}
