import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  EmergencyService,
  EmergencyType,
  EmergencyIncident,
  EmergencyContact,
  EmergencyStats,
  CreateEmergencyTypeRequest,
  UpdateEmergencyTypeRequest,
  CreateEmergencyContactRequest,
  UpdateEmergencyContactRequest
} from '../../../../core/services/emergency.service';

@Component({
  selector: 'app-emergency',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbModalModule,
    FeatherIconDirective
  ],
  templateUrl: './emergency.component.html',
  styleUrl: './emergency.component.scss'
})
export class EmergencyComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private emergencyService = inject(EmergencyService);
  private modalService = inject(NgbModal);

  @ViewChild('typeModal') typeModal!: TemplateRef<any>;
  @ViewChild('contactModal') contactModal!: TemplateRef<any>;
  @ViewChild('incidentModal') incidentModal!: TemplateRef<any>;

  // Data properties
  emergencyTypes: EmergencyType[] = [];
  incidents: EmergencyIncident[] = [];
  contacts: EmergencyContact[] = [];
  stats: EmergencyStats | null = null;
  loading = false;
  saving = false;
  error: string | null = null;
  success: string | null = null;

  // View state
  viewMode: 'types' | 'incidents' | 'contacts' = 'types';
  filteredData: any[] = [];

  // Modal properties
  selectedType: EmergencyType | null = null;
  selectedIncident: EmergencyIncident | null = null;
  selectedContact: EmergencyContact | null = null;
  isEditMode = false;
  typeForm!: FormGroup;
  contactForm!: FormGroup;
  incidentForm!: FormGroup;

  // Filter properties
  searchTerm = '';
  statusFilter = 'all';
  severityFilter = 'all';
  contactTypeFilter = 'all';

  // Configuration data
  severityLevels: Array<{value: string, label: string}> = [];
  contactTypes: Array<{value: string, label: string}> = [];
  statusTypes: Array<{value: string, label: string}> = [];

  ngOnInit(): void {
    this.initializeForms();
    this.loadConfigurationData();
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    this.typeForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      description: ['', [Validators.maxLength(500)]],
      detectionKeywords: [[]],
      severityLevel: ['High', Validators.required],
      autoEscalate: [true],
      requiresEvacuation: [false],
      contactEmergencyServices: [false],
      isActive: [true]
    });

    this.contactForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      contactType: ['', Validators.required],
      phoneNumber: ['', [Validators.required, Validators.maxLength(20)]],
      email: ['', [Validators.email]],
      address: ['', [Validators.maxLength(200)]],
      notes: ['', [Validators.maxLength(500)]],
      isPrimary: [false],
      isActive: [true]
    });

    this.incidentForm = this.fb.group({
      resolutionNotes: ['', Validators.required]
    });
  }

  private loadConfigurationData(): void {
    this.severityLevels = this.emergencyService.getSeverityLevels();
    this.contactTypes = this.emergencyService.getContactTypes();
    this.statusTypes = this.emergencyService.getStatusTypes();
  }

  loadData(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      types: this.emergencyService.getEmergencyTypes(),
      incidents: this.emergencyService.getEmergencyIncidents(),
      contacts: this.emergencyService.getEmergencyContacts(),
      stats: this.emergencyService.getStats()
    }).pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        this.emergencyTypes = data.types;
        this.incidents = data.incidents;
        this.contacts = data.contacts;
        this.stats = data.stats;
        this.applyFilters();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading emergency data:', error);
        this.error = 'Failed to load emergency data. Please try again.';
        this.loading = false;
      }
    });
  }

  setViewMode(mode: 'types' | 'incidents' | 'contacts'): void {
    this.viewMode = mode;
    this.applyFilters();
  }

  applyFilters(): void {
    let filtered: any[] = [];

    switch (this.viewMode) {
      case 'types':
        filtered = [...(this.emergencyTypes || [])];
        if (this.severityFilter !== 'all') {
          filtered = filtered.filter(type => type.severityLevel === this.severityFilter);
        }
        break;

      case 'incidents':
        filtered = [...(this.incidents || [])];
        if (this.statusFilter !== 'all') {
          filtered = filtered.filter(incident => incident.status === this.statusFilter);
        }
        if (this.severityFilter !== 'all') {
          filtered = filtered.filter(incident => incident.severityLevel === this.severityFilter);
        }
        break;

      case 'contacts':
        filtered = [...(this.contacts || [])];
        if (this.contactTypeFilter !== 'all') {
          filtered = filtered.filter(contact => contact.contactType === this.contactTypeFilter);
        }
        break;
    }

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(item => {
        const searchableText = `${item.name || item.title} ${item.description || ''}`.toLowerCase();
        return searchableText.includes(term);
      });
    }

    this.filteredData = filtered;
  }

  // Emergency Type operations
  openCreateTypeModal(): void {
    this.isEditMode = false;
    this.selectedType = null;
    this.typeForm.reset({
      severityLevel: 'High',
      autoEscalate: true,
      requiresEvacuation: false,
      contactEmergencyServices: false,
      isActive: true,
      detectionKeywords: []
    });
    this.modalService.open(this.typeModal, { size: 'lg', backdrop: 'static' });
  }

  openEditTypeModal(type: EmergencyType): void {
    this.isEditMode = true;
    this.selectedType = type;
    this.typeForm.patchValue({
      name: type.name,
      description: type.description,
      detectionKeywords: type.detectionKeywords || [],
      severityLevel: type.severityLevel,
      autoEscalate: type.autoEscalate,
      requiresEvacuation: type.requiresEvacuation,
      contactEmergencyServices: type.contactEmergencyServices,
      isActive: type.isActive
    });
    this.modalService.open(this.typeModal, { size: 'lg', backdrop: 'static' });
  }

  saveType(): void {
    if (this.typeForm.invalid) {
      this.markFormGroupTouched(this.typeForm);
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.typeForm.value;
    const typeData: CreateEmergencyTypeRequest | UpdateEmergencyTypeRequest = {
      name: formValue.name,
      description: formValue.description,
      detectionKeywords: formValue.detectionKeywords || [],
      severityLevel: formValue.severityLevel,
      autoEscalate: formValue.autoEscalate,
      requiresEvacuation: formValue.requiresEvacuation,
      contactEmergencyServices: formValue.contactEmergencyServices,
      isActive: formValue.isActive
    };

    const operation = this.isEditMode
      ? this.emergencyService.updateEmergencyType(this.selectedType!.id, typeData)
      : this.emergencyService.createEmergencyType(typeData);

    operation.pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (type) => {
          if (this.isEditMode) {
            const index = this.emergencyTypes.findIndex(t => t.id === type.id);
            if (index !== -1) {
              this.emergencyTypes[index] = type;
            }
          } else {
            this.emergencyTypes.push(type);
          }
          this.applyFilters();
          this.saving = false;
          this.success = `Emergency type ${this.isEditMode ? 'updated' : 'created'} successfully!`;
          this.modalService.dismissAll();
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving type:', error);
          this.error = this.getErrorMessage(error, 'emergency type');
          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  deleteType(type: EmergencyType): void {
    if (!confirm(`Are you sure you want to delete the emergency type "${type.name}"?`)) {
      return;
    }

    this.emergencyService.deleteEmergencyType(type.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.emergencyTypes = this.emergencyTypes.filter(t => t.id !== type.id);
          this.applyFilters();
          this.success = 'Emergency type deleted successfully!';
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error deleting type:', error);
          this.error = 'Failed to delete emergency type. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  // Emergency Contact operations
  openCreateContactModal(): void {
    this.isEditMode = false;
    this.selectedContact = null;
    this.contactForm.reset({
      isPrimary: false,
      isActive: true
    });
    this.modalService.open(this.contactModal, { size: 'lg', backdrop: 'static' });
  }

  openEditContactModal(contact: EmergencyContact): void {
    this.isEditMode = true;
    this.selectedContact = contact;
    this.contactForm.patchValue({
      name: contact.name,
      contactType: contact.contactType,
      phoneNumber: contact.phoneNumber,
      email: contact.email,
      address: contact.address,
      notes: contact.notes,
      isPrimary: contact.isPrimary,
      isActive: contact.isActive
    });
    this.modalService.open(this.contactModal, { size: 'lg', backdrop: 'static' });
  }

  saveContact(): void {
    if (this.contactForm.invalid) {
      this.markFormGroupTouched(this.contactForm);
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.contactForm.value;
    const contactData: CreateEmergencyContactRequest | UpdateEmergencyContactRequest = {
      name: formValue.name,
      contactType: formValue.contactType,
      phoneNumber: formValue.phoneNumber,
      email: formValue.email,
      address: formValue.address,
      notes: formValue.notes,
      isPrimary: formValue.isPrimary,
      isActive: formValue.isActive
    };

    const operation = this.isEditMode
      ? this.emergencyService.updateEmergencyContact(this.selectedContact!.id, contactData)
      : this.emergencyService.createEmergencyContact(contactData);

    operation.pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (contact) => {
          if (this.isEditMode) {
            const index = this.contacts.findIndex(c => c.id === contact.id);
            if (index !== -1) {
              this.contacts[index] = contact;
            }
          } else {
            this.contacts.push(contact);
          }
          this.applyFilters();
          this.saving = false;
          this.success = `Emergency contact ${this.isEditMode ? 'updated' : 'created'} successfully!`;
          this.modalService.dismissAll();
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving contact:', error);
          this.error = this.getErrorMessage(error, 'emergency contact');
          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  deleteContact(contact: EmergencyContact): void {
    if (!confirm(`Are you sure you want to delete the emergency contact "${contact.name}"?`)) {
      return;
    }

    this.emergencyService.deleteEmergencyContact(contact.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.contacts = this.contacts.filter(c => c.id !== contact.id);
          this.applyFilters();
          this.success = 'Emergency contact deleted successfully!';
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error deleting contact:', error);
          this.error = 'Failed to delete emergency contact. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  // Incident operations
  openResolveIncidentModal(incident: EmergencyIncident): void {
    this.selectedIncident = incident;
    this.incidentForm.reset();
    this.modalService.open(this.incidentModal, { size: 'lg', backdrop: 'static' });
  }

  resolveIncident(): void {
    if (this.incidentForm.invalid) {
      this.markFormGroupTouched(this.incidentForm);
      return;
    }

    this.saving = true;
    this.error = null;

    const resolutionNotes = this.incidentForm.get('resolutionNotes')?.value;

    this.emergencyService.resolveIncident(this.selectedIncident!.id, resolutionNotes)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (incident) => {
          const index = this.incidents.findIndex(i => i.id === incident.id);
          if (index !== -1) {
            this.incidents[index] = incident;
          }
          this.applyFilters();
          this.saving = false;
          this.success = 'Emergency incident resolved successfully!';
          this.modalService.dismissAll();
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error resolving incident:', error);
          this.error = this.getErrorMessage(error, 'incident');
          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  // Keyword management
  addKeyword(keyword: string, formControl: any): void {
    const currentKeywords = formControl.value || [];
    if (keyword && !currentKeywords.includes(keyword)) {
      formControl.setValue([...currentKeywords, keyword]);
    }
  }

  removeKeyword(keywordToRemove: string, formControl: any): void {
    const currentKeywords = formControl.value || [];
    formControl.setValue(currentKeywords.filter((keyword: string) => keyword !== keywordToRemove));
  }

  // Utility methods
  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(field => {
      const control = formGroup.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  private getErrorMessage(error: any, itemType: string): string {
    if (error.status === 401) {
      return 'Your session has expired. Please log in again.';
    } else if (error.status === 403) {
      return `You do not have permission to manage ${itemType}s.`;
    } else if (error.status === 400) {
      return 'Invalid data provided. Please check your input and try again.';
    } else {
      return `Failed to save ${itemType}. Please try again.`;
    }
  }

  getSeverityBadgeClass(severity: string): string {
    return this.emergencyService.getSeverityBadgeClass(severity);
  }

  getStatusBadgeClass(status: string): string {
    return this.emergencyService.getStatusBadgeClass(status);
  }

  trackById(index: number, item: any): number {
    return item.id;
  }
}