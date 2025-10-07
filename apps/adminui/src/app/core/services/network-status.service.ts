import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, fromEvent, merge } from 'rxjs';
import { map } from 'rxjs/operators';

export enum NetworkStatus {
  Online = 'online',
  Offline = 'offline',
  Unknown = 'unknown'
}

@Injectable({
  providedIn: 'root'
})
export class NetworkStatusService {
  private networkStatus$ = new BehaviorSubject<NetworkStatus>(
    this.getInitialStatus()
  );

  private connectionQuality$ = new BehaviorSubject<'fast' | 'slow' | 'unknown'>('unknown');

  constructor() {
    this.initializeNetworkMonitoring();
  }

  /**
   * Get current network status as observable
   */
  getNetworkStatus(): Observable<NetworkStatus> {
    return this.networkStatus$.asObservable();
  }

  /**
   * Get current network status value
   */
  isOnline(): boolean {
    return this.networkStatus$.value === NetworkStatus.Online;
  }

  /**
   * Get connection quality
   */
  getConnectionQuality(): Observable<'fast' | 'slow' | 'unknown'> {
    return this.connectionQuality$.asObservable();
  }

  /**
   * Initialize network monitoring
   */
  private initializeNetworkMonitoring(): void {
    // Monitor online/offline events
    merge(
      fromEvent(window, 'online').pipe(map(() => NetworkStatus.Online)),
      fromEvent(window, 'offline').pipe(map(() => NetworkStatus.Offline))
    ).subscribe((status) => {
      console.log(`Network status changed: ${status}`);
      this.networkStatus$.next(status);

      // Check connection quality when coming online
      if (status === NetworkStatus.Online) {
        this.checkConnectionQuality();
      }
    });

    // Monitor connection changes (for mobile devices)
    if ('connection' in navigator) {
      const connection = (navigator as any).connection;
      if (connection) {
        connection.addEventListener('change', () => {
          this.checkConnectionQuality();
        });
        this.checkConnectionQuality();
      }
    }

    // Periodically verify online status (every 30 seconds)
    setInterval(() => {
      if (navigator.onLine !== (this.networkStatus$.value === NetworkStatus.Online)) {
        this.networkStatus$.next(
          navigator.onLine ? NetworkStatus.Online : NetworkStatus.Offline
        );
      }
    }, 30000);
  }

  /**
   * Check connection quality using Network Information API
   */
  private checkConnectionQuality(): void {
    if (!('connection' in navigator)) {
      this.connectionQuality$.next('unknown');
      return;
    }

    const connection = (navigator as any).connection;
    if (!connection) {
      this.connectionQuality$.next('unknown');
      return;
    }

    const effectiveType = connection.effectiveType;

    // Determine quality based on effective connection type
    if (effectiveType === '4g' || effectiveType === 'wifi') {
      this.connectionQuality$.next('fast');
    } else if (effectiveType === '3g' || effectiveType === '2g' || effectiveType === 'slow-2g') {
      this.connectionQuality$.next('slow');
    } else {
      this.connectionQuality$.next('unknown');
    }

    console.log(`Connection quality: ${this.connectionQuality$.value} (${effectiveType})`);
  }

  /**
   * Get initial network status
   */
  private getInitialStatus(): NetworkStatus {
    return navigator.onLine ? NetworkStatus.Online : NetworkStatus.Offline;
  }

  /**
   * Test connectivity by making a lightweight request
   */
  async testConnectivity(): Promise<boolean> {
    try {
      const response = await fetch('/favicon.ico', {
        method: 'HEAD',
        cache: 'no-cache'
      });
      const isOnline = response.ok;
      this.networkStatus$.next(isOnline ? NetworkStatus.Online : NetworkStatus.Offline);
      return isOnline;
    } catch (error) {
      this.networkStatus$.next(NetworkStatus.Offline);
      return false;
    }
  }
}
