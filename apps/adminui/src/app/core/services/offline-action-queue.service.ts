import { Injectable } from '@angular/core';
import { IndexedDBService } from './indexed-db.service';

export interface OfflineAction {
  id: string;
  type: 'CREATE' | 'UPDATE' | 'DELETE';
  endpoint: string;
  method: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: any;
  headers?: Record<string, string>;
  status: 'pending' | 'syncing' | 'completed' | 'failed';
  createdAt: Date;
  updatedAt?: Date;
  retryCount: number;
  maxRetries: number;
  error?: string;
  entityType: 'task' | 'conversation' | 'message' | 'emergency' | 'other';
  entityId?: number | string;
}

@Injectable({
  providedIn: 'root'
})
export class OfflineActionQueueService {
  private readonly storeName = 'offlineActions';
  private readonly maxRetries = 3;

  constructor(private indexedDBService: IndexedDBService) {}

  /**
   * Add action to offline queue
   */
  async enqueueAction(
    type: OfflineAction['type'],
    endpoint: string,
    method: OfflineAction['method'],
    body?: any,
    entityType: OfflineAction['entityType'] = 'other',
    entityId?: number | string
  ): Promise<string> {
    const action: OfflineAction = {
      id: this.generateId(),
      type,
      endpoint,
      method,
      body,
      status: 'pending',
      createdAt: new Date(),
      retryCount: 0,
      maxRetries: this.maxRetries,
      entityType,
      entityId
    };

    await this.indexedDBService.saveItems(this.storeName, [action]);
    console.log(`Enqueued offline action: ${type} ${endpoint}`);

    return action.id;
  }

  /**
   * Get all pending actions
   */
  async getPendingActions(): Promise<OfflineAction[]> {
    const allActions = await this.indexedDBService.getItems<OfflineAction>(this.storeName);
    return allActions
      .filter(action => action.status === 'pending' || action.status === 'failed')
      .filter(action => action.retryCount < action.maxRetries)
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
  }

  /**
   * Get actions by entity
   */
  async getActionsByEntity(
    entityType: OfflineAction['entityType'],
    entityId: number | string
  ): Promise<OfflineAction[]> {
    const allActions = await this.indexedDBService.getItems<OfflineAction>(this.storeName);
    return allActions.filter(
      action => action.entityType === entityType && action.entityId === entityId
    );
  }

  /**
   * Update action status
   */
  async updateActionStatus(
    id: string,
    status: OfflineAction['status'],
    error?: string
  ): Promise<void> {
    const allActions = await this.indexedDBService.getItems<OfflineAction>(this.storeName);
    const action = allActions.find(a => a.id === id);

    if (!action) {
      console.warn(`Action not found: ${id}`);
      return;
    }

    action.status = status;
    action.updatedAt = new Date();

    if (error) {
      action.error = error;
    }

    if (status === 'failed') {
      action.retryCount++;
    }

    await this.indexedDBService.saveItems(this.storeName, [action]);
  }

  /**
   * Mark action as completed and remove from queue
   */
  async completeAction(id: string): Promise<void> {
    await this.updateActionStatus(id, 'completed');

    // Remove completed actions after a delay (for debugging)
    setTimeout(async () => {
      try {
        const allActions = await this.indexedDBService.getItems<OfflineAction>(this.storeName);
        const action = allActions.find(a => a.id === id);

        if (action && action.status === 'completed') {
          await this.removeAction(id);
        }
      } catch (error) {
        console.error('Error removing completed action:', error);
      }
    }, 60000); // Keep for 1 minute for debugging
  }

  /**
   * Remove action from queue
   */
  async removeAction(id: string): Promise<void> {
    const allActions = await this.indexedDBService.getItems<OfflineAction>(this.storeName);
    const remainingActions = allActions.filter(a => a.id !== id);

    await this.indexedDBService.clearStore(this.storeName);

    if (remainingActions.length > 0) {
      await this.indexedDBService.saveItems(this.storeName, remainingActions);
    }

    console.log(`Removed action from queue: ${id}`);
  }

  /**
   * Get queue statistics
   */
  async getQueueStats(): Promise<{
    total: number;
    pending: number;
    syncing: number;
    failed: number;
    completed: number;
  }> {
    const allActions = await this.indexedDBService.getItems<OfflineAction>(this.storeName);

    return {
      total: allActions.length,
      pending: allActions.filter(a => a.status === 'pending').length,
      syncing: allActions.filter(a => a.status === 'syncing').length,
      failed: allActions.filter(a => a.status === 'failed').length,
      completed: allActions.filter(a => a.status === 'completed').length
    };
  }

  /**
   * Clear all completed and expired failed actions
   */
  async cleanupQueue(): Promise<void> {
    const allActions = await this.indexedDBService.getItems<OfflineAction>(this.storeName);
    const now = new Date().getTime();
    const oneHourAgo = now - (60 * 60 * 1000);

    // Remove completed actions and failed actions older than 1 hour
    const activeActions = allActions.filter(action => {
      if (action.status === 'completed') {
        return false;
      }
      if (action.status === 'failed' && new Date(action.updatedAt || action.createdAt).getTime() < oneHourAgo) {
        return false;
      }
      return true;
    });

    await this.indexedDBService.clearStore(this.storeName);

    if (activeActions.length > 0) {
      await this.indexedDBService.saveItems(this.storeName, activeActions);
    }

    console.log(`Cleaned up queue. Removed ${allActions.length - activeActions.length} actions`);
  }

  /**
   * Clear entire queue (for testing/debugging)
   */
  async clearQueue(): Promise<void> {
    await this.indexedDBService.clearStore(this.storeName);
    console.log('Cleared entire offline action queue');
  }

  /**
   * Generate unique ID for action
   */
  private generateId(): string {
    return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Check if there are pending actions
   */
  async hasPendingActions(): Promise<boolean> {
    const pending = await this.getPendingActions();
    return pending.length > 0;
  }

  /**
   * Get count of pending actions
   */
  async getPendingCount(): Promise<number> {
    const pending = await this.getPendingActions();
    return pending.length;
  }
}
