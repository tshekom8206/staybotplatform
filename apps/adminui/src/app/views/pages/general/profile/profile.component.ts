import { Component, OnInit, inject, ViewChild, TemplateRef } from '@angular/core';
import { NgbDropdownModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { UserService, StaffMember } from '../../../../core/services/user.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    NgbDropdownModule,
    CommonModule,
    FormsModule
  ],
  templateUrl: './profile.component.html'
})
export class ProfileComponent implements OnInit {
  private userService = inject(UserService);
  private modalService = inject(NgbModal);
  private route = inject(ActivatedRoute);

  @ViewChild('editProfileModal') editProfileModal!: TemplateRef<any>;

  currentUser: StaffMember | null = null;
  loading = true;
  error: string | null = null;

  editForm: {
    email: string;
    phoneNumber: string;
  } = {
    email: '',
    phoneNumber: ''
  };

  isUpdating = false;
  updateError: string | null = null;

  ngOnInit() {
    this.loadCurrentUser();
  }

  private loadCurrentUser() {
    this.userService.getCurrentUser().subscribe({
      next: (user) => {
        this.currentUser = user;
        this.loading = false;

        // Check if we should auto-open the edit modal after user is loaded
        this.route.queryParams.subscribe(params => {
          if (params['openEditModal'] === 'true') {
            setTimeout(() => {
              this.openEditModal(this.editProfileModal);
            }, 100);
          }
        });
      },
      error: (error) => {
        console.error('Error loading current user:', error);
        this.error = 'Failed to load user profile';
        this.loading = false;
      }
    });
  }

  formatJoinDate(dateString: string | undefined): string {
    if (!dateString) return 'Unknown';
    return this.userService.formatDate(dateString);
  }

  getRoleBadgeClass(role: string | undefined): string {
    if (!role) return 'bg-secondary';
    return this.userService.getRoleBadgeClass(role);
  }

  openEditModal(content: any) {
    if (this.currentUser) {
      this.editForm = {
        email: this.currentUser.email || '',
        phoneNumber: this.currentUser.phoneNumber || ''
      };
      this.updateError = null;
      this.modalService.open(content, { size: 'lg' });
    }
  }

  saveProfile(modal: any) {
    if (!this.currentUser) return;

    this.isUpdating = true;
    this.updateError = null;

    const updateRequest = {
      email: this.editForm.email,
      role: this.currentUser.role,
      phoneNumber: this.editForm.phoneNumber,
      isActive: this.currentUser.isActive
    };

    this.userService.updateStaffMember(this.currentUser.id, updateRequest).subscribe({
      next: (updatedUser) => {
        this.currentUser = updatedUser;
        this.isUpdating = false;
        modal.close();
      },
      error: (error) => {
        console.error('Error updating profile:', error);
        this.updateError = 'Failed to update profile. Please try again.';
        this.isUpdating = false;
      }
    });
  }
}
