import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbDropdownModule, NgbDropdownToggle, NgbDropdownMenu, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  UserService,
  StaffMember,
  CreateStaffMemberRequest,
  UpdateStaffMemberRequest,
  ChangePasswordRequest,
  StaffStats,
  AvailableRole
} from '../../../../core/services/user.service';

@Component({
  selector: 'app-staff',
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
    FeatherIconDirective
  ],
  templateUrl: './staff.component.html',
  styleUrls: ['./staff.component.scss']
})
export class StaffComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private modalService = inject(NgbModal);
  private userService = inject(UserService);
  private formBuilder = inject(FormBuilder);

  @ViewChild('staffModal', { static: true }) staffModalTemplate!: TemplateRef<any>;
  @ViewChild('passwordModal', { static: true }) passwordModalTemplate!: TemplateRef<any>;

  // Data properties
  staffMembers: StaffMember[] = [];
  filteredStaffMembers: StaffMember[] = [];
  availableRoles: AvailableRole[] = [];
  stats: StaffStats | null = null;

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Search and filters
  searchTerm = '';
  selectedRole = '';
  selectedStatus = '';
  activeTab = 'all';

  // Modal state
  staffForm: FormGroup;
  passwordForm: FormGroup;
  isEditing = false;
  editingStaff: StaffMember | null = null;
  modalRef: any;

  constructor() {
    this.staffForm = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      role: ['Agent', [Validators.required]],
      phoneNumber: [''],
      isActive: [true]
    });

    this.passwordForm = this.formBuilder.group({
      newPassword: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

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
      staffMembers: this.userService.getStaffMembers(),
      availableRoles: this.userService.getAvailableRoles(),
      stats: this.userService.getStaffStats()
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (data) => {
        this.staffMembers = data.staffMembers;
        this.availableRoles = data.availableRoles;
        this.stats = data.stats;
        this.applyFilters();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading staff data:', error);
        this.error = 'Failed to load staff data. Please try again.';
        this.loading = false;
      }
    });
  }

  applyFilters() {
    let filtered = [...this.staffMembers];

    // Apply search filter
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(staff =>
        staff.email.toLowerCase().includes(term) ||
        staff.role.toLowerCase().includes(term) ||
        staff.phoneNumber.toLowerCase().includes(term)
      );
    }

    // Apply role filter
    if (this.selectedRole) {
      filtered = filtered.filter(staff => staff.role === this.selectedRole);
    }

    // Apply status filter
    if (this.selectedStatus) {
      const isActive = this.selectedStatus === 'active';
      filtered = filtered.filter(staff => staff.isActive === isActive);
    }

    // Apply tab filter
    switch (this.activeTab) {
      case 'active':
        filtered = filtered.filter(staff => staff.isActive);
        break;
      case 'inactive':
        filtered = filtered.filter(staff => !staff.isActive);
        break;
      case 'managers':
        filtered = filtered.filter(staff => staff.role === 'Manager');
        break;
      case 'agents':
        filtered = filtered.filter(staff => staff.role === 'Agent');
        break;
    }

    this.filteredStaffMembers = filtered;
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
    this.selectedRole = '';
    this.selectedStatus = '';
    this.activeTab = 'all';
    this.applyFilters();
  }

  openAddStaffModal() {
    this.isEditing = false;
    this.editingStaff = null;
    this.staffForm.reset({
      role: 'Agent',
      isActive: true
    });
    this.staffForm.get('password')?.setValidators([Validators.required, Validators.minLength(6)]);
    this.modalRef = this.modalService.open(this.staffModalTemplate, {
      backdrop: 'static',
      keyboard: false,
      size: 'lg'
    });
  }

  openEditStaffModal(staff: StaffMember) {
    this.isEditing = true;
    this.editingStaff = staff;
    this.staffForm.patchValue({
      email: staff.email,
      role: staff.role,
      phoneNumber: staff.phoneNumber,
      isActive: staff.isActive
    });
    this.staffForm.get('password')?.clearValidators();
    this.staffForm.get('password')?.updateValueAndValidity();
    this.modalRef = this.modalService.open(this.staffModalTemplate, {
      backdrop: 'static',
      keyboard: false,
      size: 'lg'
    });
  }

  openPasswordModal(staff: StaffMember) {
    this.editingStaff = staff;
    this.passwordForm.reset();
    this.modalRef = this.modalService.open(this.passwordModalTemplate, {
      backdrop: 'static',
      keyboard: false
    });
  }

  saveStaff() {
    if (this.staffForm.invalid) {
      Object.keys(this.staffForm.controls).forEach(key => {
        const control = this.staffForm.get(key);
        if (control && control.invalid) {
          control.markAsTouched();
        }
      });
      return;
    }

    const formData = this.staffForm.value;

    if (this.isEditing && this.editingStaff) {
      const updateRequest: UpdateStaffMemberRequest = {
        email: formData.email,
        role: formData.role,
        phoneNumber: formData.phoneNumber || null,
        isActive: formData.isActive
      };

      this.userService.updateStaffMember(this.editingStaff.id, updateRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.showSuccessMessage('Staff member updated successfully');
            this.closeModal();
            this.loadData();
          },
          error: (error) => {
            this.error = error.error?.message || 'Failed to update staff member';
          }
        });
    } else {
      const createRequest: CreateStaffMemberRequest = {
        email: formData.email,
        password: formData.password,
        role: formData.role,
        phoneNumber: formData.phoneNumber || undefined,
        isActive: formData.isActive
      };

      this.userService.createStaffMember(createRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.showSuccessMessage('Staff member created successfully');
            this.closeModal();
            this.loadData();
          },
          error: (error) => {
            this.error = error.error?.message || 'Failed to create staff member';
          }
        });
    }
  }

  changePassword() {
    if (this.passwordForm.invalid || !this.editingStaff) {
      return;
    }

    const request: ChangePasswordRequest = {
      newPassword: this.passwordForm.value.newPassword
    };

    this.userService.changePassword(this.editingStaff.id, request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage('Password changed successfully');
          this.closeModal();
        },
        error: (error) => {
          this.error = error.error?.message || 'Failed to change password';
        }
      });
  }

  deleteStaff(staff: StaffMember) {
    if (confirm(`Are you sure you want to delete ${staff.email}? This action cannot be undone.`)) {
      this.userService.deleteStaffMember(staff.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.showSuccessMessage('Staff member deleted successfully');
            this.loadData();
          },
          error: (error) => {
            this.error = error.error?.message || 'Failed to delete staff member';
          }
        });
    }
  }

  toggleStaffStatus(staff: StaffMember) {
    const updateRequest: UpdateStaffMemberRequest = {
      email: staff.email,
      role: staff.role,
      phoneNumber: staff.phoneNumber,
      isActive: !staff.isActive
    };

    this.userService.updateStaffMember(staff.id, updateRequest)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          const action = staff.isActive ? 'deactivated' : 'activated';
          this.showSuccessMessage(`Staff member ${action} successfully`);
          this.loadData();
        },
        error: (error) => {
          this.error = error.error?.message || 'Failed to update staff status';
        }
      });
  }

  closeModal() {
    if (this.modalRef) {
      this.modalRef.close();
      this.modalRef = null;
    }
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
  getRoleBadgeClass(role: string): string {
    return this.userService.getRoleBadgeClass(role);
  }

  getStatusBadgeClass(isActive: boolean): string {
    return this.userService.getStatusBadgeClass(isActive);
  }

  getStatusText(isActive: boolean): string {
    return this.userService.getStatusText(isActive);
  }

  formatDate(dateString: string): string {
    return this.userService.formatDate(dateString);
  }

  getTimeAgo(dateString: string): string {
    return this.userService.getTimeAgo(dateString);
  }

  getFilteredStaffCount(): number {
    return this.filteredStaffMembers.length;
  }

  getTotalStaffCount(): number {
    return this.staffMembers.length;
  }

  // Form validation helpers
  isFieldInvalid(fieldName: string, form: FormGroup = this.staffForm): boolean {
    const field = form.get(fieldName);
    return !!(field && field.invalid && field.touched);
  }

  getFieldError(fieldName: string, form: FormGroup = this.staffForm): string {
    const field = form.get(fieldName);
    if (field?.errors) {
      if (field.errors['required']) return `${fieldName} is required`;
      if (field.errors['email']) return 'Invalid email format';
      if (field.errors['minlength']) return `Minimum length is ${field.errors['minlength'].requiredLength}`;
    }
    return '';
  }

  getRoleDescription(roleValue: string): string {
    const role = this.availableRoles.find(r => r.value === roleValue);
    return role?.description || '';
  }

  getRoleIcon(role: string): string {
    switch (role.toLowerCase()) {
      case 'owner':
        return 'award';
      case 'superadmin':
        return 'shield';
      case 'admin':
        return 'settings';
      case 'manager':
        return 'briefcase';
      case 'agent':
        return 'headphones';
      default:
        return 'user';
    }
  }

  getRoleIconBgClass(role: string): string {
    switch (role.toLowerCase()) {
      case 'owner':
        return 'bg-warning';
      case 'superadmin':
        return 'bg-danger';
      case 'admin':
        return 'bg-primary';
      case 'manager':
        return 'bg-info';
      case 'agent':
        return 'bg-success';
      default:
        return 'bg-secondary';
    }
  }
}