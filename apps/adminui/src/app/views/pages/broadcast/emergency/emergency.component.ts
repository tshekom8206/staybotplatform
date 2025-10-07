import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbModalModule, NgbAlertModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../../core/services/auth.service';

export interface EmergencyIncident {
  id: number;
  title: string;
  description: string;
  status: string;
  severityLevel: string;
  reportedBy: string;
  location?: string;
  affectedAreas?: string;
  reportedAt: string;
  resolvedAt?: string;
  resolutionNotes?: string;
  emergencyType: {
    id: number;
    name: string;
    severityLevel: string;
  };
}

export interface EmergencyAlert {
  id?: string;
  type: 'fire' | 'medical' | 'security' | 'weather' | 'evacuation' | 'other';
  severity: 'low' | 'medium' | 'high' | 'critical';
  title: string;
  message: string;
  location?: string;
  instructions: string;
  status: 'draft' | 'active' | 'resolved' | 'cancelled';
  broadcastAll: boolean;
  requireAcknowledgment: boolean;
  createdAt: Date;
  resolvedAt?: Date;
  sentCount?: number;
  acknowledgedCount?: number;
}

export interface EmergencyTemplate {
  id: string;
  type: 'fire' | 'medical' | 'security' | 'weather' | 'evacuation' | 'other';
  name: string;
  title: string;
  message: string;
  instructions: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  isDefault: boolean;
}

@Component({
  selector: 'app-emergency',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbModalModule,
    NgbAlertModule,
    FeatherIconDirective
  ],
  templateUrl: './emergency.component.html',
  styleUrl: './emergency.component.scss'
})
export class EmergencyComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);

  emergencyForm: FormGroup;
  loading = false;
  error: string | null = null;
  success: string | null = null;

  // Emergency types with icons
  emergencyTypes = [
    { value: 'fire', label: 'Fire Emergency', icon: 'flame', color: '#dc3545' },
    { value: 'medical', label: 'Medical Emergency', icon: 'heart', color: '#e83e8c' },
    { value: 'security', label: 'Security Alert', icon: 'shield', color: '#fd7e14' },
    { value: 'weather', label: 'Weather Alert', icon: 'cloud-rain', color: '#20c997' },
    { value: 'evacuation', label: 'Evacuation Notice', icon: 'alert-triangle', color: '#dc3545' },
    { value: 'other', label: 'Other Emergency', icon: 'alert-circle', color: '#6f42c1' }
  ];

  // Predefined emergency templates
  emergencyTemplates: EmergencyTemplate[] = [
    {
      id: 'fire-001',
      type: 'fire',
      name: 'Fire Alarm - General',
      title: '🔥 FIRE EMERGENCY - IMMEDIATE ACTION REQUIRED',
      message: 'A fire emergency has been detected in the hotel. For your safety, please follow evacuation procedures immediately.',
      instructions: '1. Stay calm and do not panic\n2. Exit your room immediately\n3. Use stairs, DO NOT use elevators\n4. Proceed to the nearest fire exit\n5. Gather at the designated assembly point\n6. Wait for further instructions from hotel staff',
      severity: 'critical',
      isDefault: true
    },
    {
      id: 'medical-001',
      type: 'medical',
      name: 'Medical Emergency',
      title: '🏥 Medical Emergency Alert',
      message: 'We are responding to a medical emergency in the hotel. Please be aware of increased activity from emergency services.',
      instructions: '1. Remain in your room unless directed otherwise\n2. Keep corridors clear for medical personnel\n3. Contact front desk if you need assistance\n4. Follow any additional instructions from hotel staff',
      severity: 'high',
      isDefault: true
    },
    {
      id: 'security-001',
      type: 'security',
      name: 'Security Alert',
      title: '🛡️ Security Alert - Please Follow Instructions',
      message: 'We are addressing a security matter in the hotel. Your safety is our priority.',
      instructions: '1. Remain in your room and lock the door\n2. Do not open your door unless asked by hotel security\n3. Contact front desk for any assistance\n4. Monitor for further updates',
      severity: 'high',
      isDefault: true
    }
  ];

  // Current active alerts
  activeAlerts: EmergencyAlert[] = [];
  emergencyIncidents: EmergencyIncident[] = [];

  // Selected template
  selectedTemplate: EmergencyTemplate | null = null;

  constructor(private authService: AuthService) {
    this.initializeForm();
  }

  ngOnInit(): void {
    this.loadActiveAlerts();
    this.loadEmergencyIncidents();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.emergencyForm = this.fb.group({
      type: ['fire', Validators.required],
      severity: ['critical', Validators.required],
      title: ['', [Validators.required, Validators.maxLength(100)]],
      message: ['', [Validators.required, Validators.minLength(20)]],
      location: [''],
      instructions: ['', [Validators.required, Validators.minLength(20)]],
      broadcastAll: [true],
      requireAcknowledgment: [true]
    });
  }

  private async loadActiveAlerts(): Promise<void> {
    try {
      const token = this.authService.getToken();
      if (!token) {
        this.error = 'Authentication required. Please log in again.';
        return;
      }

      const response = await fetch(`${environment.apiUrl}/emergency/incidents?status=ACTIVE`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`Failed to load active alerts: ${response.status}`);
      }

      const data = await response.json();
      this.convertIncidentsToAlerts(data.incidents || []);
    } catch (error) {
      console.error('Error loading active alerts:', error);
      this.error = 'Failed to load active alerts';
      this.activeAlerts = [];
    }
  }

  private async loadEmergencyIncidents(): Promise<void> {
    try {
      const token = this.authService.getToken();
      if (!token) {
        return;
      }

      const response = await fetch(`${environment.apiUrl}/emergency/incidents`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`Failed to load emergency incidents: ${response.status}`);
      }

      const data = await response.json();
      this.emergencyIncidents = data.incidents || [];
    } catch (error) {
      console.error('Error loading emergency incidents:', error);
    }
  }

  private convertIncidentsToAlerts(incidents: EmergencyIncident[]): void {
    this.activeAlerts = incidents.map(incident => ({
      id: incident.id.toString(),
      type: this.mapEmergencyTypeToType(incident.emergencyType.name),
      severity: this.mapSeverityLevel(incident.severityLevel),
      title: incident.title,
      message: incident.description,
      location: incident.location || '',
      instructions: incident.affectedAreas || '',
      status: incident.status.toLowerCase() as 'active' | 'resolved' | 'cancelled',
      broadcastAll: true,
      requireAcknowledgment: true,
      createdAt: new Date(incident.reportedAt),
      resolvedAt: incident.resolvedAt ? new Date(incident.resolvedAt) : undefined,
      sentCount: 0,
      acknowledgedCount: 0
    }));
  }

  private mapEmergencyTypeToType(typeName: string): 'fire' | 'medical' | 'security' | 'weather' | 'evacuation' | 'other' {
    const lowerName = typeName.toLowerCase();
    if (lowerName.includes('fire')) return 'fire';
    if (lowerName.includes('medical') || lowerName.includes('health')) return 'medical';
    if (lowerName.includes('security') || lowerName.includes('theft')) return 'security';
    if (lowerName.includes('weather') || lowerName.includes('storm')) return 'weather';
    if (lowerName.includes('evacuation')) return 'evacuation';
    return 'other';
  }

  private mapSeverityLevel(severity: string): 'low' | 'medium' | 'high' | 'critical' {
    const lowerSeverity = severity.toLowerCase();
    if (lowerSeverity === 'critical') return 'critical';
    if (lowerSeverity === 'high') return 'high';
    if (lowerSeverity === 'medium' || lowerSeverity === 'moderate') return 'medium';
    return 'low';
  }

  applyTemplate(template: EmergencyTemplate): void {
    this.selectedTemplate = template;
    this.emergencyForm.patchValue({
      type: template.type,
      severity: template.severity,
      title: template.title,
      message: template.message,
      instructions: template.instructions
    });
  }

  clearTemplate(): void {
    this.selectedTemplate = null;
    this.emergencyForm.reset({
      type: 'fire',
      severity: 'critical',
      broadcastAll: true,
      requireAcknowledgment: true
    });
  }

  getTypeConfig(type: string) {
    return this.emergencyTypes.find(t => t.value === type) || this.emergencyTypes[0];
  }

  getSeverityClass(severity: string): string {
    switch (severity) {
      case 'critical': return 'badge bg-danger';
      case 'high': return 'badge bg-warning';
      case 'medium': return 'badge bg-info';
      case 'low': return 'badge bg-secondary';
      default: return 'badge bg-danger';
    }
  }

  getSeverityIcon(severity: string): string {
    switch (severity) {
      case 'critical': return 'alert-triangle';
      case 'high': return 'alert-circle';
      case 'medium': return 'info';
      case 'low': return 'minus-circle';
      default: return 'alert-triangle';
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'active': return 'badge bg-danger';
      case 'resolved': return 'badge bg-success';
      case 'cancelled': return 'badge bg-secondary';
      case 'draft': return 'badge bg-warning';
      default: return 'badge bg-secondary';
    }
  }

  async sendEmergencyAlert(): Promise<void> {
    if (this.emergencyForm.invalid) {
      this.markFormGroupTouched();
      return;
    }

    this.loading = true;
    this.error = null;

    try {
      const token = this.authService.getToken();
      if (!token) {
        throw new Error('Authentication required. Please log in again.');
      }

      const formData = {
        title: this.emergencyForm.get('title')?.value,
        description: this.emergencyForm.get('message')?.value,
        location: this.emergencyForm.get('location')?.value,
        affectedAreas: this.emergencyForm.get('instructions')?.value,
        severityLevel: this.emergencyForm.get('severity')?.value.toUpperCase(),
        emergencyTypeId: this.getEmergencyTypeId(this.emergencyForm.get('type')?.value)
      };

      const response = await fetch(`${environment.apiUrl}/emergency/incidents`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(formData)
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(errorData?.message || `Failed to create emergency alert: ${response.status}`);
      }

      this.success = 'Emergency alert created successfully!';
      this.clearTemplate();
      await this.loadActiveAlerts();

      setTimeout(() => this.success = null, 5000);
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'Failed to create emergency alert';
      console.error('Error creating emergency alert:', error);
    } finally {
      this.loading = false;
    }
  }

  private getEmergencyTypeId(type: string): number {
    // Default emergency type IDs - these should match your database
    const typeMap: { [key: string]: number } = {
      'fire': 1,
      'medical': 2,
      'security': 3,
      'weather': 4,
      'evacuation': 5,
      'other': 6
    };
    return typeMap[type] || 1;
  }

  async resolveAlert(alertId: string): Promise<void> {
    try {
      const token = this.authService.getToken();
      if (!token) {
        this.error = 'Authentication required. Please log in again.';
        return;
      }

      const response = await fetch(`${environment.apiUrl}/emergency/incidents/${alertId}/resolve`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          resolutionNotes: 'Emergency resolved via admin interface'
        })
      });

      if (!response.ok) {
        throw new Error(`Failed to resolve alert: ${response.status}`);
      }

      this.success = 'Emergency alert resolved successfully';
      await this.loadActiveAlerts();
      setTimeout(() => this.success = null, 3000);
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'Failed to resolve alert';
      console.error('Error resolving alert:', error);
    }
  }

  async cancelAlert(alertId: string): Promise<void> {
    try {
      const token = this.authService.getToken();
      if (!token) {
        this.error = 'Authentication required. Please log in again.';
        return;
      }

      const response = await fetch(`${environment.apiUrl}/emergency/incidents/${alertId}/resolve`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          resolutionNotes: 'Emergency cancelled - false alarm'
        })
      });

      if (!response.ok) {
        throw new Error(`Failed to cancel alert: ${response.status}`);
      }

      this.success = 'Emergency alert cancelled successfully';
      await this.loadActiveAlerts();
      setTimeout(() => this.success = null, 3000);
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'Failed to cancel alert';
      console.error('Error cancelling alert:', error);
    }
  }

  getTimeAgo(date: Date): string {
    const now = new Date();
    const diffInMinutes = Math.floor((now.getTime() - date.getTime()) / (1000 * 60));

    if (diffInMinutes < 1) return 'Just now';
    if (diffInMinutes < 60) return `${diffInMinutes}m ago`;

    const diffInHours = Math.floor(diffInMinutes / 60);
    if (diffInHours < 24) return `${diffInHours}h ago`;

    const diffInDays = Math.floor(diffInHours / 24);
    return `${diffInDays}d ago`;
  }

  getAcknowledgmentPercentage(alert: EmergencyAlert): number {
    if (!alert.sentCount || !alert.acknowledgedCount) return 0;
    return Math.round((alert.acknowledgedCount / alert.sentCount) * 100);
  }

  private markFormGroupTouched(): void {
    Object.keys(this.emergencyForm.controls).forEach(field => {
      const control = this.emergencyForm.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  get isFormValid(): boolean {
    return this.emergencyForm.valid;
  }

  get criticalWarning(): boolean {
    return this.emergencyForm.get('severity')?.value === 'critical';
  }

  trackByAlertId(index: number, alert: EmergencyAlert): string {
    return alert.id || index.toString();
  }
}