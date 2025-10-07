import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbDropdownModule, NgbDropdownToggle, NgbDropdownMenu, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal, NgbAccordionModule, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionButton, NgbAccordionCollapse, NgbAccordionBody } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  RolesService,
  Role,
  Permission,
  PermissionCategory,
  RolePermissions,
  RoleStats
} from '../../../../core/services/roles.service';

@Component({
  selector: 'app-roles',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbDropdownToggle,
    NgbDropdownMenu,
    NgbTooltipModule,
    NgbAlertModule,
    NgbModalModule,
    NgbAccordionModule,
    NgbAccordionDirective,
    NgbAccordionItem,
    NgbAccordionHeader,
    NgbAccordionButton,
    NgbAccordionCollapse,
    NgbAccordionBody,
    FeatherIconDirective
  ],
  templateUrl: './roles.component.html',
  styleUrls: ['./roles.component.scss']
})
export class RolesComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private modalService = inject(NgbModal);
  private rolesService = inject(RolesService);
  private formBuilder = inject(FormBuilder);

  @ViewChild('permissionsModal', { static: true }) permissionsModalTemplate!: TemplateRef<any>;

  // Data properties
  roles: Role[] = [];
  filteredRoles: Role[] = [];
  permissionCategories: PermissionCategory[] = [];
  allPermissions: Permission[] = [];
  stats: RoleStats | null = null;

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Search and filters
  searchTerm = '';
  selectedRoleType = '';
  activeTab = 'all';

  // Modal state
  selectedRole: Role | null = null;
  rolePermissions: RolePermissions | null = null;
  modalRef: any;

  constructor() {}

  ngOnInit() {
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    this.loading = true;
    this.error = null;

    forkJoin({
      roles: this.rolesService.getRoles(),
      permissions: this.rolesService.getPermissions(),
      stats: this.rolesService.getRoleStats()
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (data) => {
        this.roles = data.roles;
        this.permissionCategories = data.permissions.categories;
        this.allPermissions = data.permissions.permissions;
        this.stats = data.stats;
        this.applyFilters();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading roles data:', error);
        this.error = 'Failed to load roles data. Please try again.';
        this.loading = false;
      }
    });
  }

  applyFilters() {
    let filtered = [...this.roles];

    // Apply search filter
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(role =>
        role.name.toLowerCase().includes(term) ||
        role.displayName.toLowerCase().includes(term) ||
        role.description.toLowerCase().includes(term)
      );
    }

    // Apply role type filter
    if (this.selectedRoleType) {
      if (this.selectedRoleType === 'builtin') {
        filtered = filtered.filter(role => role.isBuiltIn);
      } else if (this.selectedRoleType === 'custom') {
        filtered = filtered.filter(role => !role.isBuiltIn);
      }
    }

    // Apply tab filter
    switch (this.activeTab) {
      case 'builtin':
        filtered = filtered.filter(role => role.isBuiltIn);
        break;
      case 'custom':
        filtered = filtered.filter(role => !role.isBuiltIn);
        break;
      case 'active':
        filtered = filtered.filter(role => role.userCount > 0);
        break;
    }

    this.filteredRoles = filtered;
  }

  onSearch() {
    this.applyFilters();
  }

  onFilterChange() {
    this.applyFilters();
  }

  setActiveTab(tab: string) {
    this.activeTab = tab;
    this.applyFilters();
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedRoleType = '';
    this.activeTab = 'all';
    this.applyFilters();
  }

  openPermissionsModal(role: Role) {
    this.selectedRole = role;
    this.rolePermissions = null;

    // Load detailed permissions for this role
    this.rolesService.getRolePermissions(role.name)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (permissions) => {
          this.rolePermissions = permissions;
        },
        error: (error) => {
          console.error('Error loading role permissions:', error);
          this.error = 'Failed to load role permissions';
        }
      });

    this.modalRef = this.modalService.open(this.permissionsModalTemplate, {
      backdrop: 'static',
      keyboard: false,
      size: 'xl'
    });
  }

  closeModal() {
    if (this.modalRef) {
      this.modalRef.close();
      this.modalRef = null;
    }
    this.selectedRole = null;
    this.rolePermissions = null;
    this.error = null;
  }

  showSuccessMessage(message: string) {
    this.successMessage = message;
    setTimeout(() => {
      this.successMessage = null;
    }, 5000);
  }

  dismissError() {
    this.error = null;
  }

  dismissSuccess() {
    this.successMessage = null;
  }

  // Utility methods
  getRoleBadgeClass(roleName: string): string {
    return this.rolesService.getRoleBadgeClass(roleName);
  }

  getRoleIcon(roleName: string): string {
    return this.rolesService.getRoleIcon(roleName);
  }

  getPermissionCategoryIcon(category: string): string {
    return this.rolesService.getPermissionCategoryIcon(category);
  }

  getPermissionCategoryColor(category: string): string {
    return this.rolesService.getPermissionCategoryColor(category);
  }

  formatPermissionCount(count: number): string {
    return this.rolesService.formatPermissionCount(count);
  }

  formatUserCount(count: number): string {
    return this.rolesService.formatUserCount(count);
  }

  getPermissionDescription(permissionName: string): string {
    return this.rolesService.getPermissionDescription(permissionName);
  }

  getFilteredRolesCount(): number {
    return this.filteredRoles.length;
  }

  getTotalRolesCount(): number {
    return this.roles.length;
  }

  getBuiltInRolesCount(): number {
    return this.roles.filter(role => role.isBuiltIn).length;
  }

  getCustomRolesCount(): number {
    return this.roles.filter(role => !role.isBuiltIn).length;
  }

  getActiveRolesCount(): number {
    return this.roles.filter(role => role.userCount > 0).length;
  }

  getPermissionsByCategory(permissions: string[], category: string): string[] {
    return permissions.filter(permission => permission.startsWith(`${category}.`));
  }

  hasPermissionInCategory(rolePermissions: string[], category: string): boolean {
    return rolePermissions.some(permission => permission.startsWith(`${category}.`));
  }

  getPermissionCountInCategory(rolePermissions: string[], category: string): number {
    return this.getPermissionsByCategory(rolePermissions, category).length;
  }

  getTotalPermissionsInCategory(category: PermissionCategory): number {
    return category.permissions.length;
  }

  getCategoryPermissionSummary(rolePermissions: string[], category: PermissionCategory): string {
    const granted = this.getPermissionCountInCategory(rolePermissions, category.name);
    const total = category.permissions.length;

    if (granted === 0) return 'No access';
    if (granted === total) return 'Full access';
    return `${granted}/${total} permissions`;
  }

  getCategoryAccessLevel(rolePermissions: string[], category: PermissionCategory): 'none' | 'partial' | 'full' {
    const granted = this.getPermissionCountInCategory(rolePermissions, category.name);
    const total = category.permissions.length;

    if (granted === 0) return 'none';
    if (granted === total) return 'full';
    return 'partial';
  }

  getCategoryBadgeClass(level: 'none' | 'partial' | 'full'): string {
    switch (level) {
      case 'none': return 'bg-secondary';
      case 'partial': return 'bg-warning';
      case 'full': return 'bg-success';
      default: return 'bg-secondary';
    }
  }

  // Comparison utilities
  compareRoles(role1: Role, role2: Role): void {
    const comparison = this.rolesService.compareRolePermissions(role1, role2);

    console.log('Role Comparison:', {
      role1: role1.name,
      role2: role2.name,
      common: comparison.common,
      unique1: comparison.unique1,
      unique2: comparison.unique2
    });

    // Could show this in a modal or expand the UI to show comparison
    this.showSuccessMessage(`Compared ${role1.name} and ${role2.name}. Check console for details.`);
  }

  // Export functionality (placeholder)
  exportRoleDefinitions() {
    const exportData = {
      roles: this.roles.map(role => ({
        name: role.name,
        displayName: role.displayName,
        description: role.description,
        permissions: role.permissions,
        isBuiltIn: role.isBuiltIn,
        userCount: role.userCount
      })),
      permissionCategories: this.permissionCategories,
      exportDate: new Date().toISOString()
    };

    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `roles-permissions-${new Date().toISOString().split('T')[0]}.json`;
    link.click();
    window.URL.revokeObjectURL(url);

    this.showSuccessMessage('Role definitions exported successfully');
  }

  // Permission search within modal
  searchPermissions(searchTerm: string): void {
    if (!searchTerm.trim() || !this.rolePermissions) {
      return;
    }

    // This could be enhanced to filter the visible permissions in the modal
    console.log('Searching permissions for:', searchTerm);
  }

  // Helper methods for template to avoid arrow functions
  getCategorySummaryText(category: any): string {
    if (!this.selectedRole?.permissions) return 'No access';

    const categoryName = category.name;
    const permissions = this.selectedRole.permissions.filter(p => p.startsWith(`${categoryName}.`));
    const total = category.permissions.length;

    if (permissions.length === 0) return 'No access';
    if (permissions.length === total) return 'Full access';
    return `${permissions.length}/${total} permissions`;
  }

  getCategoryAccessClass(category: any): string {
    if (!this.selectedRole?.permissions) return 'bg-secondary';

    const categoryName = category.name;
    const permissions = this.selectedRole.permissions.filter(p => p.startsWith(`${categoryName}.`));
    const total = category.permissions.length;

    if (permissions.length === 0) return 'bg-secondary';
    if (permissions.length === total) return 'bg-success';
    return 'bg-warning';
  }

  getTotalPermissionsCount(): number {
    return this.selectedRole?.permissions.length || 0;
  }
}