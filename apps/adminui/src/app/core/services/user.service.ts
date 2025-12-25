import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface StaffMember {
  id: number;
  email: string;
  userName: string;
  isActive: boolean;
  createdAt: string;
  role: string;
  phoneNumber: string;
  roleAssignedAt: string;
}

export interface CreateStaffMemberRequest {
  email: string;
  password: string;
  role: string;
  phoneNumber?: string;
  isActive?: boolean;
}

export interface UpdateStaffMemberRequest {
  email: string;
  role: string;
  phoneNumber?: string;
  isActive: boolean;
}

export interface ChangePasswordRequest {
  newPassword: string;
}

export interface StaffStats {
  totalStaff: number;
  activeStaff: number;
  inactiveStaff: number;
  staffByRole: Record<string, number>;
  recentActivity: StaffActivity[];
}

export interface StaffActivity {
  staffEmail: string;
  action: string;
  timestamp: string;
}

export interface AvailableRole {
  value: string;
  label: string;
  description: string;
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private apiUrl = `${environment.apiUrl}/staff`;

  // Staff Management endpoints
  getStaffMembers(role?: string): Observable<StaffMember[]> {
    let params: any = {};
    if (role) {
      params.role = role;
    }
    return this.http.get<{ staff: StaffMember[] }>(`${this.apiUrl}`, { params })
      .pipe(map(response => response.staff));
  }

  getStaffMember(id: number): Observable<StaffMember> {
    return this.http.get<StaffMember>(`${this.apiUrl}/${id}`);
  }

  getCurrentUser(): Observable<StaffMember> {
    // Get current user email from auth service
    const currentUser = this.authService.currentUserValue;
    if (!currentUser?.email) {
      return throwError(() => new Error('No current user found'));
    }

    // Get all staff members and find the current user
    return this.http.get<{ staff: StaffMember[] }>(`${this.apiUrl}`).pipe(
      map(response => {
        const currentStaff = response.staff.find(staff => staff.email === currentUser.email);
        if (!currentStaff) {
          throw new Error('Current user not found in staff list');
        }
        return currentStaff;
      })
    );
  }

  createStaffMember(staff: CreateStaffMemberRequest): Observable<StaffMember> {
    return this.http.post<StaffMember>(`${this.apiUrl}`, staff);
  }

  updateStaffMember(id: number, staff: UpdateStaffMemberRequest): Observable<StaffMember>;
  updateStaffMember(staff: { id: number; userName: string; email: string; phoneNumber: string; }): Observable<StaffMember>;
  updateStaffMember(
    idOrStaff: number | { id: number; userName: string; email: string; phoneNumber: string; },
    staff?: UpdateStaffMemberRequest
  ): Observable<StaffMember> {
    if (typeof idOrStaff === 'number') {
      return this.http.put<StaffMember>(`${this.apiUrl}/${idOrStaff}`, staff);
    } else {
      const { id, ...updateData } = idOrStaff;
      return this.http.put<StaffMember>(`${this.apiUrl}/${id}`, updateData);
    }
  }

  deleteStaffMember(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.apiUrl}/${id}`);
  }

  changePassword(id: number, request: ChangePasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/${id}/change-password`, request);
  }

  // Available roles endpoint
  getAvailableRoles(): Observable<AvailableRole[]> {
    return this.http.get<{ roles: AvailableRole[] }>(`${this.apiUrl}/roles`)
      .pipe(map(response => response.roles));
  }

  // Statistics endpoint
  getStaffStats(): Observable<StaffStats> {
    return this.http.get<StaffStats>(`${this.apiUrl}/stats`);
  }

  // Utility methods
  getRoleBadgeClass(role: string): string {
    switch (role.toLowerCase()) {
      case 'owner': return 'bg-primary';
      case 'manager': return 'bg-success';
      case 'agent': return 'bg-info';
      case 'superadmin': return 'bg-warning';
      default: return 'bg-secondary';
    }
  }

  getStatusBadgeClass(isActive: boolean): string {
    return isActive ? 'bg-success' : 'bg-danger';
  }

  getStatusText(isActive: boolean): string {
    return isActive ? 'Active' : 'Inactive';
  }

  validateEmail(email: string): boolean {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
  }

  validatePassword(password: string): { isValid: boolean; errors: string[] } {
    const errors: string[] = [];

    if (password.length < 6) {
      errors.push('Password must be at least 6 characters long');
    }

    if (!/[A-Z]/.test(password)) {
      errors.push('Password must contain at least one uppercase letter');
    }

    if (!/[a-z]/.test(password)) {
      errors.push('Password must contain at least one lowercase letter');
    }

    if (!/\d/.test(password)) {
      errors.push('Password must contain at least one number');
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-ZA', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Africa/Johannesburg'
    });
  }

  getTimeAgo(dateString: string): string {
    const now = new Date();
    const date = new Date(dateString);
    const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffInSeconds < 60) {
      return 'Just now';
    } else if (diffInSeconds < 3600) {
      const minutes = Math.floor(diffInSeconds / 60);
      return `${minutes} minute${minutes !== 1 ? 's' : ''} ago`;
    } else if (diffInSeconds < 86400) {
      const hours = Math.floor(diffInSeconds / 3600);
      return `${hours} hour${hours !== 1 ? 's' : ''} ago`;
    } else if (diffInSeconds < 2592000) {
      const days = Math.floor(diffInSeconds / 86400);
      return `${days} day${days !== 1 ? 's' : ''} ago`;
    } else {
      return this.formatDate(dateString);
    }
  }

  // Bulk operations (for future enhancement)
  bulkUpdateStaffStatus(ids: number[], isActive: boolean): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/bulk/status`, { ids, isActive });
  }

  bulkAssignRole(ids: number[], role: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/bulk/role`, { ids, role });
  }

  // Export functionality
  exportStaffList(format: 'csv' | 'excel' = 'csv'): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export?format=${format}`, {
      responseType: 'blob'
    });
  }
}
