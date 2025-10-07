import { Injectable, ApplicationRef } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { BehaviorSubject, Observable, concat, interval } from 'rxjs';
import { first, filter } from 'rxjs/operators';

export interface UpdateStatus {
  available: boolean;
  downloading: boolean;
  current?: string;
  available_version?: string;
  message?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ServiceWorkerUpdateService {
  private updateAvailable$ = new BehaviorSubject<UpdateStatus>({
    available: false,
    downloading: false
  });

  private isInitialized = false;

  constructor(
    private swUpdate: SwUpdate,
    private appRef: ApplicationRef
  ) {}

  /**
   * Initialize service worker update monitoring
   */
  async initialize(): Promise<void> {
    if (this.isInitialized) {
      return;
    }

    if (!this.swUpdate.isEnabled) {
      console.log('Service Worker not enabled');
      return;
    }

    console.log('Initializing Service Worker Update Service');

    // Check for updates when app becomes stable
    const appIsStable$ = this.appRef.isStable.pipe(
      first(isStable => isStable === true)
    );

    // Check for updates every 6 hours
    const everySixHours$ = interval(6 * 60 * 60 * 1000);

    const everySixHoursOnceAppIsStable$ = concat(appIsStable$, everySixHours$);

    everySixHoursOnceAppIsStable$.subscribe(async () => {
      try {
        await this.checkForUpdate();
      } catch (error) {
        console.error('Failed to check for updates:', error);
      }
    });

    // Listen for version updates
    this.swUpdate.versionUpdates
      .pipe(filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'))
      .subscribe(event => {
        console.log('New version available:', event.latestVersion);
        this.updateAvailable$.next({
          available: true,
          downloading: false,
          current: event.currentVersion.hash,
          available_version: event.latestVersion.hash,
          message: 'A new version of the app is available!'
        });
      });

    // Listen for unrecoverable state
    this.swUpdate.unrecoverable.subscribe(event => {
      console.error('Service Worker unrecoverable state:', event);
      this.updateAvailable$.next({
        available: false,
        downloading: false,
        message: 'App is in an unrecoverable state. Please reload.'
      });
    });

    this.isInitialized = true;

    // Check immediately on initialization
    await this.checkForUpdate();
  }

  /**
   * Get update status as observable
   */
  getUpdateStatus(): Observable<UpdateStatus> {
    return this.updateAvailable$.asObservable();
  }

  /**
   * Check for available updates
   */
  async checkForUpdate(): Promise<boolean> {
    if (!this.swUpdate.isEnabled) {
      console.log('Service Worker not enabled - cannot check for updates');
      return false;
    }

    try {
      console.log('Checking for app updates...');
      const updateAvailable = await this.swUpdate.checkForUpdate();

      if (updateAvailable) {
        console.log('Update check: new version available');
      } else {
        console.log('Update check: already on latest version');
      }

      return updateAvailable;
    } catch (error) {
      console.error('Error checking for updates:', error);
      return false;
    }
  }

  /**
   * Activate pending update and reload
   */
  async activateUpdate(): Promise<void> {
    if (!this.swUpdate.isEnabled) {
      console.log('Service Worker not enabled');
      return;
    }

    try {
      console.log('Activating update...');

      this.updateAvailable$.next({
        ...this.updateAvailable$.value,
        downloading: true,
        message: 'Updating...'
      });

      await this.swUpdate.activateUpdate();

      console.log('Update activated - reloading page');

      // Reload the page to load the new version
      window.location.reload();
    } catch (error) {
      console.error('Error activating update:', error);
      this.updateAvailable$.next({
        ...this.updateAvailable$.value,
        downloading: false,
        message: 'Failed to update. Please refresh manually.'
      });
    }
  }

  /**
   * Dismiss update notification
   */
  dismissUpdate(): void {
    this.updateAvailable$.next({
      available: false,
      downloading: false
    });
  }

  /**
   * Check if service worker is enabled
   */
  isEnabled(): boolean {
    return this.swUpdate.isEnabled;
  }

  /**
   * Force reload the page
   */
  forceReload(): void {
    window.location.reload();
  }

  /**
   * Get current service worker registration
   */
  async getRegistration(): Promise<ServiceWorkerRegistration | undefined> {
    if (!('serviceWorker' in navigator)) {
      return undefined;
    }

    try {
      return await navigator.serviceWorker.getRegistration();
    } catch (error) {
      console.error('Error getting service worker registration:', error);
      return undefined;
    }
  }

  /**
   * Unregister service worker (for debugging)
   */
  async unregisterServiceWorker(): Promise<boolean> {
    if (!('serviceWorker' in navigator)) {
      return false;
    }

    try {
      const registration = await navigator.serviceWorker.getRegistration();
      if (registration) {
        const success = await registration.unregister();
        console.log('Service worker unregistered:', success);
        return success;
      }
      return false;
    } catch (error) {
      console.error('Error unregistering service worker:', error);
      return false;
    }
  }
}
