import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface Role {
  name: string;
  displayName: string;
  description: string;
  permissions: string[];
  userCount: number;
  isBuiltIn: boolean;
  createdAt: string;
}

export interface Permission {
  name: string;
  displayName: string;
  category: string;
  description: string;
}

export interface PermissionCategory {
  name: string;
  displayName: string;
  permissions: Permission[];
}

export interface RolePermissionDetails {
  name: string;
  displayName: string;
  description: string;
  hasPermission: boolean;
}

export interface RolePermissionCategory {
  name: string;
  displayName: string;
  permissions: RolePermissionDetails[];
}

export interface RolePermissions {
  role: string;
  displayName: string;
  permissions: RolePermissionCategory[];
}

export interface CreateRoleRequest {
  name: string;
  displayName: string;
  description: string;
  permissions: string[];
}

export interface UpdateRoleRequest {
  displayName: string;
  description: string;
  permissions: string[];
}

export interface RoleStats {
  totalRoles: number;
  builtInRoles: number;
  customRoles: number;
  usersByRole: Record<string, number>;
  mostUsedPermissions: PermissionUsage[];
}

export interface PermissionUsage {
  permission: string;
  displayName: string;
  roleCount: number;
}

export interface PermissionValidation {
  valid: string[];
  invalid: string[];
  allValid: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class RolesService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/roles`;

  // Role Management endpoints
  getRoles(): Observable<Role[]> {
    return this.http.get<{roles: Role[]}>(`${this.apiUrl}`)
      .pipe(map(response => response.roles));
  }

  getRole(roleName: string): Observable<Role> {
    return this.http.get<Role>(`${this.apiUrl}/${roleName}`);
  }

  createRole(role: CreateRoleRequest): Observable<Role> {
    return this.http.post<Role>(`${this.apiUrl}`, role);
  }

  updateRole(roleName: string, role: UpdateRoleRequest): Observable<Role> {
    return this.http.put<Role>(`${this.apiUrl}/${roleName}`, role);
  }

  deleteRole(roleName: string): Observable<{message: string}> {
    return this.http.delete<{message: string}>(`${this.apiUrl}/${roleName}`);
  }

  // Permission Management endpoints
  getPermissions(): Observable<{categories: PermissionCategory[], permissions: Permission[]}> {
    return this.http.get<{categories: PermissionCategory[], permissions: Permission[]}>(`${this.apiUrl}/permissions`);
  }

  getRolePermissions(roleName: string): Observable<RolePermissions> {
    return this.http.get<RolePermissions>(`${this.apiUrl}/permissions/${roleName}`);
  }

  validatePermissions(permissions: string[]): Observable<PermissionValidation> {
    return this.http.post<PermissionValidation>(`${this.apiUrl}/validate-permissions`, permissions);
  }

  // Statistics endpoint
  getRoleStats(): Observable<RoleStats> {
    return this.http.get<RoleStats>(`${this.apiUrl}/stats`);
  }

  // Utility methods
  getRoleBadgeClass(roleName: string): string {
    switch (roleName.toLowerCase()) {
      case 'owner': return 'bg-primary';
      case 'manager': return 'bg-success';
      case 'agent': return 'bg-info';
      case 'superadmin': return 'bg-warning';
      default: return 'bg-secondary';
    }
  }

  getRoleIcon(roleName: string): string {
    switch (roleName.toLowerCase()) {
      case 'owner': return 'crown';
      case 'manager': return 'users';
      case 'agent': return 'user';
      case 'superadmin': return 'shield';
      default: return 'user-check';
    }
  }

  getPermissionCategoryIcon(category: string): string {
    switch (category.toLowerCase()) {
      case 'users': return 'users';
      case 'roles': return 'shield';
      case 'guests': return 'user-check';
      case 'tasks': return 'clipboard';
      case 'broadcast': return 'radio';
      case 'configuration': return 'settings';
      case 'reports': return 'bar-chart-2';
      case 'billing': return 'credit-card';
      case 'system': return 'server';
      default: return 'lock';
    }
  }

  getPermissionCategoryColor(category: string): string {
    switch (category.toLowerCase()) {
      case 'users': return 'primary';
      case 'roles': return 'warning';
      case 'guests': return 'info';
      case 'tasks': return 'success';
      case 'broadcast': return 'danger';
      case 'configuration': return 'secondary';
      case 'reports': return 'dark';
      case 'billing': return 'success';
      case 'system': return 'danger';
      default: return 'secondary';
    }
  }

  formatPermissionCount(count: number): string {
    if (count === 1) return '1 permission';
    return `${count} permissions`;
  }

  formatUserCount(count: number): string {
    if (count === 0) return 'No users';
    if (count === 1) return '1 user';
    return `${count} users`;
  }

  getPermissionDescription(permissionName: string): string {
    const descriptions: Record<string, string> = {
      'users.view': 'View staff and user information',
      'users.create': 'Add new staff members',
      'users.edit': 'Modify user information and roles',
      'users.delete': 'Remove users from the system',
      'roles.view': 'View role definitions and permissions',
      'roles.create': 'Create custom roles',
      'roles.edit': 'Modify role permissions',
      'roles.delete': 'Remove custom roles',
      'guests.view': 'View guest information and conversations',
      'guests.edit': 'Modify guest information',
      'guests.delete': 'Remove guest records',
      'tasks.view': 'View tasks and requests',
      'tasks.create': 'Create new tasks',
      'tasks.edit': 'Modify task details',
      'tasks.delete': 'Remove tasks',
      'tasks.assign': 'Assign tasks to staff members',
      'broadcast.view': 'View broadcast history',
      'broadcast.send': 'Send messages to guests',
      'broadcast.emergency': 'Send emergency alerts',
      'configuration.view': 'View system settings',
      'configuration.edit': 'Modify system settings',
      'reports.view': 'View analytics and reports',
      'reports.export': 'Export report data',
      'billing.view': 'View billing information',
      'billing.manage': 'Manage subscription and payment',
      'system.admin': 'Full system administration access',
      'system.debug': 'Access debug information',
      'system.monitor': 'Monitor system performance',
      'audit.view': 'View system audit logs',
      'tenant.manage': 'Manage tenant settings'
    };

    return descriptions[permissionName] || 'System permission';
  }

  // Permission checking utilities
  hasPermission(role: Role, permission: string): boolean {
    return role.permissions.includes(permission);
  }

  hasAnyPermission(role: Role, permissions: string[]): boolean {
    return permissions.some(permission => role.permissions.includes(permission));
  }

  hasAllPermissions(role: Role, permissions: string[]): boolean {
    return permissions.every(permission => role.permissions.includes(permission));
  }

  getPermissionsByCategory(role: Role, category: string): string[] {
    return role.permissions.filter(permission => permission.startsWith(`${category}.`));
  }

  compareRolePermissions(role1: Role, role2: Role): {
    common: string[];
    unique1: string[];
    unique2: string[];
  } {
    const set1 = new Set(role1.permissions);
    const set2 = new Set(role2.permissions);

    const common = role1.permissions.filter(p => set2.has(p));
    const unique1 = role1.permissions.filter(p => !set2.has(p));
    const unique2 = role2.permissions.filter(p => !set1.has(p));

    return { common, unique1, unique2 };
  }

  // Search and filtering utilities
  filterRolesByPermission(roles: Role[], permission: string): Role[] {
    return roles.filter(role => role.permissions.includes(permission));
  }

  searchRoles(roles: Role[], searchTerm: string): Role[] {
    if (!searchTerm.trim()) return roles;

    const term = searchTerm.toLowerCase();
    return roles.filter(role =>
      role.name.toLowerCase().includes(term) ||
      role.displayName.toLowerCase().includes(term) ||
      role.description.toLowerCase().includes(term)
    );
  }

  searchPermissions(permissions: Permission[], searchTerm: string): Permission[] {
    if (!searchTerm.trim()) return permissions;

    const term = searchTerm.toLowerCase();
    return permissions.filter(permission =>
      permission.name.toLowerCase().includes(term) ||
      permission.displayName.toLowerCase().includes(term) ||
      permission.description.toLowerCase().includes(term)
    );
  }

  // Role hierarchy utilities (for future enhancement)
  getRoleHierarchy(): string[] {
    return ['SuperAdmin', 'Owner', 'Manager', 'Agent'];
  }

  isHigherRole(role1: string, role2: string): boolean {
    const hierarchy = this.getRoleHierarchy();
    const index1 = hierarchy.indexOf(role1);
    const index2 = hierarchy.indexOf(role2);

    return index1 < index2 && index1 !== -1;
  }

  canManageRole(currentUserRole: string, targetRole: string): boolean {
    return this.isHigherRole(currentUserRole, targetRole) || currentUserRole === targetRole;
  }
}