import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, Observable, interval, Subscription } from 'rxjs';
import { switchMap, filter, take } from 'rxjs/operators';
import { NetworkStatusService, NetworkStatus } from './network-status.service';
import { OfflineActionQueueService, OfflineAction } from './offline-action-queue.service';

export interface SyncStatus {
  isSyncing: boolean;
  pendingCount: number;
  lastSyncTime?: Date;
  lastSyncSuccess: boolean;
  currentAction?: string;
}

@Injectable({
  providedIn: 'root'
})
export class BackgroundSyncService {
  private syncStatus$ = new BehaviorSubject<SyncStatus>({
    isSyncing: false,
    pendingCount: 0,
    lastSyncSuccess: true
  });

  private syncInterval$?: Subscription;
  private isInitialized = false;

  constructor(
    private http: HttpClient,
    private networkStatusService: NetworkStatusService,
    private offlineActionQueueService: OfflineActionQueueService
  ) {}

  /**
   * Initialize background sync service
   */
  async initialize(): Promise<void> {
    if (this.isInitialized) {
      return;
    }

    console.log('Initializing Background Sync Service');

    // Listen for network status changes
    this.networkStatusService.getNetworkStatus().subscribe(async (status) => {
      if (status === NetworkStatus.Online) {
        console.log('Network is online - triggering sync');
        await this.syncPendingActions();
      }
    });

    // Periodic sync every 5 minutes when online
    this.syncInterval$ = interval(5 * 60 * 1000)
      .pipe(
        filter(() => this.networkStatusService.isOnline()),
        switchMap(() => this.syncPendingActions())
      )
      .subscribe();

    // Initial sync if online
    if (this.networkStatusService.isOnline()) {
      await this.syncPendingActions();
    }

    // Cleanup queue periodically (every hour)
    setInterval(() => {
      this.offlineActionQueueService.cleanupQueue();
    }, 60 * 60 * 1000);

    this.isInitialized = true;
  }

  /**
   * Get sync status as observable
   */
  getSyncStatus(): Observable<SyncStatus> {
    return this.syncStatus$.asObservable();
  }

  /**
   * Manually trigger sync
   */
  async syncNow(): Promise<void> {
    if (!this.networkStatusService.isOnline()) {
      console.warn('Cannot sync - network is offline');
      return;
    }

    await this.syncPendingActions();
  }

  /**
   * Sync all pending actions
   */
  private async syncPendingActions(): Promise<void> {
    // Prevent concurrent syncs
    if (this.syncStatus$.value.isSyncing) {
      console.log('Sync already in progress');
      return;
    }

    const pendingActions = await this.offlineActionQueueService.getPendingActions();

    if (pendingActions.length === 0) {
      console.log('No pending actions to sync');
      this.updateSyncStatus({
        isSyncing: false,
        pendingCount: 0,
        lastSyncTime: new Date(),
        lastSyncSuccess: true
      });
      return;
    }

    console.log(`Starting sync of ${pendingActions.length} pending actions`);

    this.updateSyncStatus({
      isSyncing: true,
      pendingCount: pendingActions.length,
      lastSyncSuccess: true
    });

    let successCount = 0;
    let failCount = 0;

    // Process actions sequentially to maintain order
    for (const action of pendingActions) {
      try {
        await this.syncAction(action);
        successCount++;
      } catch (error) {
        console.error(`Failed to sync action ${action.id}:`, error);
        failCount++;

        // Update action as failed
        await this.offlineActionQueueService.updateActionStatus(
          action.id,
          'failed',
          error instanceof Error ? error.message : 'Unknown error'
        );
      }
    }

    console.log(`Sync completed: ${successCount} succeeded, ${failCount} failed`);

    // Update final status
    const remainingPending = await this.offlineActionQueueService.getPendingCount();

    this.updateSyncStatus({
      isSyncing: false,
      pendingCount: remainingPending,
      lastSyncTime: new Date(),
      lastSyncSuccess: failCount === 0
    });
  }

  /**
   * Sync a single action
   */
  private async syncAction(action: OfflineAction): Promise<void> {
    console.log(`Syncing action: ${action.type} ${action.endpoint}`);

    // Update action status to syncing
    await this.offlineActionQueueService.updateActionStatus(action.id, 'syncing');

    // Update current action in status
    this.updateSyncStatus({
      ...this.syncStatus$.value,
      currentAction: `${action.type} ${action.entityType}`
    });

    // Prepare headers
    const headers = new HttpHeaders(action.headers || {});

    // Execute HTTP request
    try {
      let response: any;

      switch (action.method) {
        case 'POST':
          response = await this.http.post(action.endpoint, action.body, { headers }).toPromise();
          break;
        case 'PUT':
          response = await this.http.put(action.endpoint, action.body, { headers }).toPromise();
          break;
        case 'PATCH':
          response = await this.http.patch(action.endpoint, action.body, { headers }).toPromise();
          break;
        case 'DELETE':
          response = await this.http.delete(action.endpoint, { headers }).toPromise();
          break;
      }

      // Mark action as completed
      await this.offlineActionQueueService.completeAction(action.id);

      console.log(`Successfully synced action ${action.id}`);
    } catch (error: any) {
      // Check if it's a network error or server error
      if (error.status === 0 || !navigator.onLine) {
        // Network error - don't increment retry count
        await this.offlineActionQueueService.updateActionStatus(
          action.id,
          'pending'
        );
      } else {
        // Server error - increment retry count
        throw error;
      }
    }
  }

  /**
   * Update sync status
   */
  private updateSyncStatus(status: Partial<SyncStatus>): void {
    this.syncStatus$.next({
      ...this.syncStatus$.value,
      ...status
    });
  }

  /**
   * Stop background sync
   */
  destroy(): void {
    if (this.syncInterval$) {
      this.syncInterval$.unsubscribe();
    }
    this.isInitialized = false;
  }

  /**
   * Check if sync is supported
   */
  isSyncSupported(): boolean {
    return 'serviceWorker' in navigator && 'SyncManager' in window;
  }

  /**
   * Register background sync with service worker (if supported)
   */
  async registerBackgroundSync(tag: string = 'sync-offline-actions'): Promise<void> {
    if (!this.isSyncSupported()) {
      console.warn('Background Sync API not supported');
      return;
    }

    try {
      const registration = await navigator.serviceWorker.ready;
      await (registration as any).sync.register(tag);
      console.log('Background sync registered with service worker');
    } catch (error) {
      console.error('Failed to register background sync:', error);
    }
  }

  /**
   * Get count of pending actions
   */
  async getPendingCount(): Promise<number> {
    return await this.offlineActionQueueService.getPendingCount();
  }
}
