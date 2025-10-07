import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface EmergencyType {
  id: number;
  name: string;
  description?: string;
  detectionKeywords: string[];
  severityLevel: string;
  autoEscalate: boolean;
  requiresEvacuation: boolean;
  contactEmergencyServices: boolean;
  isActive: boolean;
  updatedAt: string;
  protocolCount?: number;
  incidentCount?: number;
}

export interface EmergencyIncident {
  id: number;
  title: string;
  description: string;
  status: string;
  severityLevel: string;
  reportedBy?: string;
  location?: string;
  affectedAreas: string[];
  reportedAt: string;
  resolvedAt?: string;
  resolutionNotes?: string;
  emergencyType: {
    id: number;
    name: string;
    severityLevel: string;
  };
}

export interface EmergencyContact {
  id: number;
  name: string;
  contactType: string;
  phoneNumber: string;
  email?: string;
  address?: string;
  notes?: string;
  isPrimary: boolean;
  isActive: boolean;
  updatedAt: string;
}

export interface EmergencyStats {
  totalTypes: number;
  totalIncidents: number;
  activeIncidents: number;
  resolvedIncidents: number;
  totalContacts: number;
  incidentsBySeverity: Array<{ severityLevel: string; count: number }>;
  incidentsByType: Array<{ emergencyType: string; count: number }>;
}

export interface CreateEmergencyTypeRequest {
  name: string;
  description?: string;
  detectionKeywords?: string[];
  severityLevel?: string;
  autoEscalate?: boolean;
  requiresEvacuation?: boolean;
  contactEmergencyServices?: boolean;
  isActive?: boolean;
}

export interface UpdateEmergencyTypeRequest {
  name: string;
  description?: string;
  detectionKeywords?: string[];
  severityLevel?: string;
  autoEscalate?: boolean;
  requiresEvacuation?: boolean;
  contactEmergencyServices?: boolean;
  isActive?: boolean;
}

export interface CreateEmergencyContactRequest {
  name: string;
  contactType: string;
  phoneNumber: string;
  email?: string;
  address?: string;
  notes?: string;
  isPrimary?: boolean;
  isActive?: boolean;
}

export interface UpdateEmergencyContactRequest {
  name: string;
  contactType: string;
  phoneNumber: string;
  email?: string;
  address?: string;
  notes?: string;
  isPrimary?: boolean;
  isActive?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class EmergencyService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/emergency`;

  // Emergency Types endpoints
  getEmergencyTypes(): Observable<EmergencyType[]> {
    return this.http.get<{types: EmergencyType[]}>(`${this.apiUrl}/types`)
      .pipe(map(response => response.types));
  }

  getEmergencyType(id: number): Observable<EmergencyType> {
    return this.http.get<EmergencyType>(`${this.apiUrl}/types/${id}`);
  }

  createEmergencyType(type: CreateEmergencyTypeRequest): Observable<EmergencyType> {
    return this.http.post<EmergencyType>(`${this.apiUrl}/types`, type);
  }

  updateEmergencyType(id: number, type: UpdateEmergencyTypeRequest): Observable<EmergencyType> {
    return this.http.put<EmergencyType>(`${this.apiUrl}/types/${id}`, type);
  }

  deleteEmergencyType(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/types/${id}`);
  }

  // Emergency Incidents endpoints
  getEmergencyIncidents(filters?: {
    status?: string;
    emergencyTypeId?: number;
    severityLevel?: string;
  }): Observable<EmergencyIncident[]> {
    return this.http.get<{incidents: EmergencyIncident[]}>(`${this.apiUrl}/incidents`, { params: filters as any })
      .pipe(map(response => response.incidents));
  }

  resolveIncident(id: number, resolutionNotes: string): Observable<EmergencyIncident> {
    return this.http.put<EmergencyIncident>(`${this.apiUrl}/incidents/${id}/resolve`, { resolutionNotes });
  }

  // Emergency Contacts endpoints
  getEmergencyContacts(contactType?: string): Observable<EmergencyContact[]> {
    let params: any = {};
    if (contactType) {
      params.contactType = contactType;
    }
    return this.http.get<{contacts: EmergencyContact[]}>(`${this.apiUrl}/contacts`, { params })
      .pipe(map(response => response.contacts));
  }

  createEmergencyContact(contact: CreateEmergencyContactRequest): Observable<EmergencyContact> {
    return this.http.post<EmergencyContact>(`${this.apiUrl}/contacts`, contact);
  }

  updateEmergencyContact(id: number, contact: UpdateEmergencyContactRequest): Observable<EmergencyContact> {
    return this.http.put<EmergencyContact>(`${this.apiUrl}/contacts/${id}`, contact);
  }

  deleteEmergencyContact(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/contacts/${id}`);
  }

  // Stats endpoint
  getStats(): Observable<EmergencyStats> {
    return this.http.get<EmergencyStats>(`${this.apiUrl}/stats`);
  }

  // Utility methods
  getSeverityLevels(): Array<{value: string, label: string}> {
    return [
      { value: 'Low', label: 'Low' },
      { value: 'Medium', label: 'Medium' },
      { value: 'High', label: 'High' },
      { value: 'Critical', label: 'Critical' }
    ];
  }

  getContactTypes(): Array<{value: string, label: string}> {
    return [
      { value: 'Fire Department', label: 'Fire Department' },
      { value: 'Police', label: 'Police' },
      { value: 'Medical', label: 'Medical/Ambulance' },
      { value: 'Security', label: 'Security Company' },
      { value: 'Manager', label: 'Hotel Manager' },
      { value: 'Maintenance', label: 'Maintenance' },
      { value: 'Other', label: 'Other' }
    ];
  }

  getStatusTypes(): Array<{value: string, label: string}> {
    return [
      { value: 'ACTIVE', label: 'Active' },
      { value: 'RESOLVED', label: 'Resolved' },
      { value: 'FALSE_ALARM', label: 'False Alarm' }
    ];
  }

  getSeverityBadgeClass(severity: string): string {
    switch (severity.toLowerCase()) {
      case 'critical': return 'bg-danger';
      case 'high': return 'bg-warning';
      case 'medium': return 'bg-info';
      case 'low': return 'bg-secondary';
      default: return 'bg-secondary';
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'active': return 'bg-danger';
      case 'resolved': return 'bg-success';
      case 'false_alarm': return 'bg-secondary';
      default: return 'bg-secondary';
    }
  }
}