import { Injectable } from '@angular/core';
import { StaffTask } from '../models/task.model';

export interface IndexedDBConfig {
  dbName: string;
  version: number;
  stores: {
    name: string;
    keyPath: string;
    indexes?: { name: string; keyPath: string; unique: boolean }[];
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class IndexedDBService {
  private db: IDBDatabase | null = null;
  private readonly config: IndexedDBConfig = {
    dbName: 'staybot-admin-db',
    version: 2,
    stores: [
      {
        name: 'tasks',
        keyPath: 'id',
        indexes: [
          { name: 'department', keyPath: 'department', unique: false },
          { name: 'status', keyPath: 'status', unique: false },
          { name: 'assignedToId', keyPath: 'assignedToId', unique: false },
          { name: 'createdAt', keyPath: 'createdAt', unique: false }
        ]
      },
      {
        name: 'conversations',
        keyPath: 'id',
        indexes: [
          { name: 'status', keyPath: 'status', unique: false },
          { name: 'phoneNumber', keyPath: 'phoneNumber', unique: false },
          { name: 'createdAt', keyPath: 'createdAt', unique: false }
        ]
      },
      {
        name: 'messages',
        keyPath: 'id',
        indexes: [
          { name: 'conversationId', keyPath: 'conversationId', unique: false },
          { name: 'createdAt', keyPath: 'createdAt', unique: false }
        ]
      },
      {
        name: 'emergencies',
        keyPath: 'id',
        indexes: [
          { name: 'status', keyPath: 'status', unique: false },
          { name: 'severityLevel', keyPath: 'severityLevel', unique: false },
          { name: 'reportedAt', keyPath: 'reportedAt', unique: false }
        ]
      },
      {
        name: 'offlineActions',
        keyPath: 'id',
        indexes: [
          { name: 'type', keyPath: 'type', unique: false },
          { name: 'status', keyPath: 'status', unique: false },
          { name: 'createdAt', keyPath: 'createdAt', unique: false },
          { name: 'retryCount', keyPath: 'retryCount', unique: false }
        ]
      }
    ]
  };

  constructor() {
    this.initializeDB();
  }

  private async initializeDB(): Promise<void> {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.config.dbName, this.config.version);

      request.onerror = () => {
        console.error('IndexedDB failed to open', request.error);
        reject(request.error);
      };

      request.onsuccess = () => {
        this.db = request.result;
        console.log('IndexedDB opened successfully');
        resolve();
      };

      request.onupgradeneeded = (event: IDBVersionChangeEvent) => {
        const db = (event.target as IDBOpenDBRequest).result;

        // Create object stores
        this.config.stores.forEach(store => {
          if (!db.objectStoreNames.contains(store.name)) {
            const objectStore = db.createObjectStore(store.name, { keyPath: store.keyPath });

            // Create indexes
            store.indexes?.forEach(index => {
              objectStore.createIndex(index.name, index.keyPath, { unique: index.unique });
            });

            console.log(`Created object store: ${store.name}`);
          }
        });
      };
    });
  }

  async saveItems<T>(storeName: string, items: T[]): Promise<void> {
    if (!this.db) {
      await this.initializeDB();
    }

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readwrite');
      const store = transaction.objectStore(storeName);

      items.forEach(item => {
        store.put(item);
      });

      transaction.oncomplete = () => {
        console.log(`Saved ${items.length} items to ${storeName}`);
        resolve();
      };

      transaction.onerror = () => {
        console.error(`Failed to save items to ${storeName}`, transaction.error);
        reject(transaction.error);
      };
    });
  }

  async getItems<T>(storeName: string, limit?: number): Promise<T[]> {
    if (!this.db) {
      await this.initializeDB();
    }

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readonly');
      const store = transaction.objectStore(storeName);
      const request = store.getAll(limit);

      request.onsuccess = () => {
        resolve(request.result as T[]);
      };

      request.onerror = () => {
        console.error(`Failed to get items from ${storeName}`, request.error);
        reject(request.error);
      };
    });
  }

  async getItemById<T>(storeName: string, id: number): Promise<T | null> {
    if (!this.db) {
      await this.initializeDB();
    }

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readonly');
      const store = transaction.objectStore(storeName);
      const request = store.get(id);

      request.onsuccess = () => {
        resolve(request.result as T || null);
      };

      request.onerror = () => {
        console.error(`Failed to get item from ${storeName}`, request.error);
        reject(request.error);
      };
    });
  }

  async getItemsByIndex<T>(
    storeName: string,
    indexName: string,
    value: any,
    limit?: number
  ): Promise<T[]> {
    if (!this.db) {
      await this.initializeDB();
    }

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readonly');
      const store = transaction.objectStore(storeName);
      const index = store.index(indexName);
      const request = index.getAll(value, limit);

      request.onsuccess = () => {
        resolve(request.result as T[]);
      };

      request.onerror = () => {
        console.error(`Failed to get items by index from ${storeName}`, request.error);
        reject(request.error);
      };
    });
  }

  async deleteItem(storeName: string, id: number): Promise<void> {
    if (!this.db) {
      await this.initializeDB();
    }

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readwrite');
      const store = transaction.objectStore(storeName);
      const request = store.delete(id);

      request.onsuccess = () => {
        resolve();
      };

      request.onerror = () => {
        console.error(`Failed to delete item from ${storeName}`, request.error);
        reject(request.error);
      };
    });
  }

  async clearStore(storeName: string): Promise<void> {
    if (!this.db) {
      await this.initializeDB();
    }

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readwrite');
      const store = transaction.objectStore(storeName);
      const request = store.clear();

      request.onsuccess = () => {
        console.log(`Cleared all items from ${storeName}`);
        resolve();
      };

      request.onerror = () => {
        console.error(`Failed to clear ${storeName}`, request.error);
        reject(request.error);
      };
    });
  }

  async getCount(storeName: string): Promise<number> {
    if (!this.db) {
      await this.initializeDB();
    }

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readonly');
      const store = transaction.objectStore(storeName);
      const request = store.count();

      request.onsuccess = () => {
        resolve(request.result);
      };

      request.onerror = () => {
        console.error(`Failed to count items in ${storeName}`, request.error);
        reject(request.error);
      };
    });
  }
}
