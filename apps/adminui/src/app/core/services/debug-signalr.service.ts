import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface SignalRDebugInfo {
  connectionState: string;
  hubUrl: string;
  isAuthenticated: boolean;
  token: string | null;
  tenantId: number | null;
  lastError: string | null;
  connectionAttempts: number;
  lastConnectionAttempt: Date | null;
  connectionLogs: string[];
}

@Injectable({
  providedIn: 'root'
})
export class DebugSignalRService {
  private debugInfoSubject = new BehaviorSubject<SignalRDebugInfo>({
    connectionState: 'Not Started',
    hubUrl: '',
    isAuthenticated: false,
    token: null,
    tenantId: null,
    lastError: null,
    connectionAttempts: 0,
    lastConnectionAttempt: null,
    connectionLogs: []
  });

  public debugInfo$ = this.debugInfoSubject.asObservable();

  updateDebugInfo(updates: Partial<SignalRDebugInfo>): void {
    const current = this.debugInfoSubject.value;
    const updated = { ...current, ...updates };
    this.debugInfoSubject.next(updated);
  }

  addLog(message: string): void {
    const current = this.debugInfoSubject.value;
    const timestamp = new Date().toLocaleTimeString('en-ZA', { timeZone: 'Africa/Johannesburg' });
    const logEntry = `[${timestamp}] ${message}`;

    const updatedLogs = [...current.connectionLogs, logEntry];
    // Keep only last 50 logs
    if (updatedLogs.length > 50) {
      updatedLogs.splice(0, updatedLogs.length - 50);
    }

    this.updateDebugInfo({ connectionLogs: updatedLogs });
    console.log('🔧 SignalR Debug:', logEntry);
  }

  getDebugInfo(): SignalRDebugInfo {
    return this.debugInfoSubject.value;
  }

  clearLogs(): void {
    this.updateDebugInfo({ connectionLogs: [] });
  }
}
