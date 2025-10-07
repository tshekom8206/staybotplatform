import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { SoundService } from './sound.service';
import { DebugSignalRService } from './debug-signalr.service';

// Note: Install @microsoft/signalr package
// npm install @microsoft/signalr

export enum ConnectionState {
  Disconnected = 'Disconnected',
  Connecting = 'Connecting',
  Connected = 'Connected',
  Reconnecting = 'Reconnecting',
  Failed = 'Failed'
}

export interface TaskNotification {
  taskId: number;
  title: string;
  department: string;
  priority: string;
  roomNumber?: string;
  action: 'Created' | 'Updated' | 'Completed';
  timestamp: Date;
}

export interface EmergencyNotification {
  incidentId: number;
  type: string;
  severity: string;
  message: string;
  affectedAreas: string[];
  timestamp: Date;
}

export interface MessageNotification {
  messageId: number;
  conversationId: number;
  phoneNumber: string;
  guestName?: string;
  messageText: string;
  direction: 'Inbound' | 'Outbound';
  timestamp: Date;
}

export interface ConversationStatusNotification {
  conversationId: number;
  status: string;
  updatedBy: string;
  timestamp: Date;
}

export interface BookingNotification {
  bookingId: number;
  guestName: string;
  roomNumber: string;
  action: 'Created' | 'Updated' | 'CheckedIn' | 'CheckedOut';
  timestamp: Date;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: any; // HubConnection from @microsoft/signalr
  private connectionStateSubject = new BehaviorSubject<ConnectionState>(ConnectionState.Disconnected);

  // Event subjects
  private taskCreatedSubject = new Subject<TaskNotification>();
  private taskUpdatedSubject = new Subject<TaskNotification>();
  private taskCompletedSubject = new Subject<TaskNotification>();
  private taskAssignedSubject = new Subject<TaskNotification>();
  private emergencyAlertSubject = new Subject<EmergencyNotification>();
  private messageReceivedSubject = new Subject<MessageNotification>();
  private conversationStatusChangedSubject = new Subject<ConversationStatusNotification>();
  private bookingUpdatedSubject = new Subject<BookingNotification>();
  private notificationSubject = new Subject<any>();

  // Public observables
  public connectionState$ = this.connectionStateSubject.asObservable();
  public taskCreated$ = this.taskCreatedSubject.asObservable();
  public taskUpdated$ = this.taskUpdatedSubject.asObservable();
  public taskCompleted$ = this.taskCompletedSubject.asObservable();
  public taskAssigned$ = this.taskAssignedSubject.asObservable();
  public emergencyAlert$ = this.emergencyAlertSubject.asObservable();
  public messageReceived$ = this.messageReceivedSubject.asObservable();
  public conversationStatusChanged$ = this.conversationStatusChangedSubject.asObservable();
  public bookingUpdated$ = this.bookingUpdatedSubject.asObservable();
  public notification$ = this.notificationSubject.asObservable();

  constructor(
    private authService: AuthService,
    private soundService: SoundService,
    private debugService: DebugSignalRService
  ) {
    this.debugService.addLog('SignalRService constructor called');
  }

  /**
   * Initialize SignalR connection
   */
  async startConnection(): Promise<void> {
    this.debugService.addLog('startConnection() method called');

    if (this.hubConnection) {
      this.debugService.addLog('Existing connection found, stopping it first');
      await this.stopConnection();
    }

    const token = this.authService.getToken();
    const tenant = this.authService.currentTenantValue;
    const isAuthenticated = this.authService.isAuthenticated;

    this.debugService.updateDebugInfo({
      hubUrl: environment.hubUrl,
      isAuthenticated,
      token: token ? token.substring(0, 20) + '...' : null,
      tenantId: tenant?.id || null,
      connectionAttempts: this.debugService.getDebugInfo().connectionAttempts + 1,
      lastConnectionAttempt: new Date()
    });

    if (!token) {
      const error = 'No auth token available for SignalR connection';
      console.error(error);
      this.debugService.addLog(`ERROR: ${error}`);
      this.debugService.updateDebugInfo({ lastError: error });
      return;
    }

    this.debugService.addLog(`Auth token available: ${token.substring(0, 20)}...`);
    this.debugService.addLog(`Is authenticated: ${isAuthenticated}`);
    this.debugService.addLog(`Current tenant: ${tenant?.id || 'none'}`);

    try {
      this.debugService.addLog('Importing @microsoft/signalr package...');
      const signalR = await import('@microsoft/signalr');
      this.debugService.addLog('SignalR package imported successfully');

      this.debugService.addLog(`Building SignalR connection to: ${environment.hubUrl}`);
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(environment.hubUrl, {
          accessTokenFactory: () => {
            this.debugService.addLog('Access token factory called');
            return token;
          }
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.debugService.addLog('SignalR connection built, setting up event handlers...');

      // Set up event handlers
      this.setupEventHandlers();

      // Set up connection state handlers
      this.setupConnectionHandlers();

      // Start the connection
      this.connectionStateSubject.next(ConnectionState.Connecting);
      this.debugService.updateDebugInfo({ connectionState: ConnectionState.Connecting });

      console.log('🔗 Starting SignalR connection to:', environment.hubUrl);
      this.debugService.addLog('Attempting to start SignalR connection...');

      await this.hubConnection.start();

      this.connectionStateSubject.next(ConnectionState.Connected);
      this.debugService.updateDebugInfo({ connectionState: ConnectionState.Connected, lastError: null });
      this.debugService.addLog('SignalR connection started successfully!');

      // Join tenant group
      console.log('👤 Current tenant:', tenant);
      if (tenant) {
        this.debugService.addLog(`Joining tenant group for tenant ID: ${tenant.id}`);
        await this.joinTenantGroup(tenant.id);
      } else {
        this.debugService.addLog('WARNING: No tenant available, not joining any group');
      }

      console.log('✅ SignalR connection established successfully');
      this.debugService.addLog('✅ SignalR connection setup completed successfully');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      console.error('Error starting SignalR connection:', error);
      this.debugService.addLog(`ERROR starting SignalR connection: ${errorMessage}`);
      this.debugService.updateDebugInfo({
        connectionState: ConnectionState.Failed,
        lastError: errorMessage
      });
      this.connectionStateSubject.next(ConnectionState.Failed);
    }
  }

  /**
   * Stop SignalR connection
   */
  async stopConnection(): Promise<void> {
    if (this.hubConnection) {
      try {
        await this.hubConnection.stop();
        this.connectionStateSubject.next(ConnectionState.Disconnected);
        console.log('SignalR connection stopped');
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      }
    }
  }

  /**
   * Join tenant group for receiving tenant-specific notifications
   */
  async joinTenantGroup(tenantId: number): Promise<void> {
    if (this.hubConnection && this.connectionStateSubject.value === ConnectionState.Connected) {
      try {
        await this.hubConnection.invoke('JoinTenantGroup', tenantId);
        console.log(`Joined tenant group: ${tenantId}`);
      } catch (error) {
        console.error('Error joining tenant group:', error);
      }
    }
  }

  /**
   * Leave tenant group
   */
  async leaveTenantGroup(tenantId: number): Promise<void> {
    if (this.hubConnection && this.connectionStateSubject.value === ConnectionState.Connected) {
      try {
        await this.hubConnection.invoke('LeaveTenantGroup', tenantId);
        console.log(`Left tenant group: ${tenantId}`);
      } catch (error) {
        console.error('Error leaving tenant group:', error);
      }
    }
  }

  /**
   * Set up SignalR event handlers
   */
  private setupEventHandlers(): void {
    if (!this.hubConnection) return;

    // Task events
    this.hubConnection.on('TaskCreated', (notification: TaskNotification) => {
      console.log('🔔 SignalR TaskCreated event received:', notification);
      this.debugService.addLog(`TaskCreated event received: ${JSON.stringify(notification)}`);
      this.taskCreatedSubject.next(notification);

      // Play service bell sound for new tasks
      console.log('🎵 Attempting to play service bell sound...');
      this.debugService.addLog('Attempting to play service bell sound for new task');
      this.soundService.playServiceBell().then(() => {
        console.log('✅ Service bell sound played successfully');
        this.debugService.addLog('✅ Service bell sound played successfully');
      }).catch(error => {
        console.error('❌ Failed to play service bell sound:', error);
        this.debugService.addLog(`❌ Failed to play service bell sound: ${error}`);
      });
    });

    this.hubConnection.on('TaskUpdated', (notification: TaskNotification) => {
      console.log('Task updated:', notification);
      this.taskUpdatedSubject.next(notification);
    });

    this.hubConnection.on('TaskCompleted', (notification: TaskNotification) => {
      console.log('Task completed:', notification);
      this.taskCompletedSubject.next(notification);
    });

    this.hubConnection.on('TaskAssigned', (notification: TaskNotification) => {
      console.log('Task assigned:', notification);
      this.taskAssignedSubject.next(notification);
    });

    // Emergency events
    this.hubConnection.on('EmergencyAlert', (notification: EmergencyNotification) => {
      console.log('Emergency alert:', notification);
      this.emergencyAlertSubject.next(notification);

      // Play emergency alert sound
      this.soundService.playEmergencyAlert().catch(error => {
        console.warn('Failed to play emergency alert sound:', error);
      });
    });

    // Message events
    this.hubConnection.on('MessageReceived', (notification: MessageNotification) => {
      console.log('Message received:', notification);
      this.messageReceivedSubject.next(notification);
    });

    // Conversation events
    this.hubConnection.on('ConversationStatusChanged', (notification: ConversationStatusNotification) => {
      console.log('Conversation status changed:', notification);
      this.conversationStatusChangedSubject.next(notification);
    });

    // Booking events
    this.hubConnection.on('BookingUpdated', (notification: BookingNotification) => {
      console.log('Booking updated:', notification);
      this.bookingUpdatedSubject.next(notification);
    });

    // General notifications
    this.hubConnection.on('Notification', (notification: any) => {
      console.log('Notification received:', notification);
      this.notificationSubject.next(notification);
    });
  }

  /**
   * Set up connection state handlers
   */
  private setupConnectionHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.onreconnecting(() => {
      console.log('SignalR reconnecting...');
      this.connectionStateSubject.next(ConnectionState.Reconnecting);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR reconnected');
      this.connectionStateSubject.next(ConnectionState.Connected);

      // Rejoin tenant group after reconnection
      const tenant = this.authService.currentTenantValue;
      if (tenant) {
        this.joinTenantGroup(tenant.id);
      }
    });

    this.hubConnection.onclose(() => {
      console.log('SignalR connection closed');
      this.connectionStateSubject.next(ConnectionState.Disconnected);
    });
  }

  /**
   * Get current connection state
   */
  getConnectionState(): ConnectionState {
    return this.connectionStateSubject.value;
  }

  /**
   * Check if connected
   */
  isConnected(): boolean {
    return this.connectionStateSubject.value === ConnectionState.Connected;
  }
}