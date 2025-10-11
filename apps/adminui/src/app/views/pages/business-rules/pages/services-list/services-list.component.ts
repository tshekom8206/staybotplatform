import { Component, OnInit, OnDestroy, AfterViewChecked, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbModal, NgbTooltipModule, NgbAccordionModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import { BusinessRulesService } from '../../services/business-rules.service';
import {
  Service,
  ServiceBusinessRule,
  BusinessRulesFilter,
  CreateServiceRuleRequest,
  UpdateServiceRuleRequest
} from '../../models/business-rules.models';
import * as feather from 'feather-icons';

@Component({
  selector: 'app-services-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    NgbTooltipModule,
    NgbAccordionModule,
    FeatherIconDirective
  ],
  templateUrl: './services-list.component.html',
  styleUrl: './services-list.component.scss'
})
export class ServicesListComponent implements OnInit, OnDestroy, AfterViewChecked {
  private destroy$ = new Subject<void>();
  private businessRulesService = inject(BusinessRulesService);
  private modalService = inject(NgbModal);
  private formBuilder = inject(FormBuilder);

  @ViewChild('ruleEditorModal') ruleEditorModal!: TemplateRef<any>;
  @ViewChild('deleteConfirmModal') deleteConfirmModal!: TemplateRef<any>;

  // Data properties
  services: Service[] = [];
  filteredServices: Service[] = [];
  selectedService: Service | null = null;
  serviceRules: Map<number, ServiceBusinessRule[]> = new Map();
  expandedServiceIds: Set<number> = new Set();
  loading = true;
  loadingRules = false;
  error: string | null = null;

  // Filter properties
  searchTerm = '';
  categoryFilter = 'all';
  statusFilter = 'all';
  sortBy: 'name' | 'category' | 'ruleCount' | 'lastModified' = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Categories (will be loaded from backend in production)
  categories = ['Wellness', 'Dining', 'Amenities', 'Concierge', 'Transportation'];

  // Modal properties
  ruleForm!: FormGroup;
  selectedRule: ServiceBusinessRule | null = null;
  isEditMode = false;
  ruleToDelete: { serviceId: number; ruleId: number; ruleName: string } | null = null;

  // Tenant info
  tenantId = 1; // Will be retrieved from AuthService

  // Rule type options
  ruleTypes = [
    { value: 'MaxGroupSize', label: 'Maximum Group Size', icon: 'users' },
    { value: 'MinAdvanceHours', label: 'Minimum Advance Booking Hours', icon: 'clock' },
    { value: 'RestrictedHours', label: 'Restricted Service Hours', icon: 'calendar' },
    { value: 'CustomValidation', label: 'Custom Validation', icon: 'shield' }
  ];

  ngOnInit(): void {
    this.initializeForm();
    this.loadServices();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngAfterViewChecked(): void {
    feather.replace();
  }

  private initializeForm(): void {
    this.ruleForm = this.formBuilder.group({
      ruleType: ['', Validators.required],
      ruleKey: ['', [Validators.required, Validators.pattern(/^[a-z_]+$/)]],
      ruleValue: ['', [Validators.required, this.jsonValidator]],
      validationMessage: ['', [Validators.required, Validators.maxLength(500)]],
      priority: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
      isActive: [true],
      minConfidenceScore: [0.8, [Validators.min(0), Validators.max(1)]]
    });

    // Auto-generate ruleKey when ruleType changes
    this.ruleForm.get('ruleType')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(ruleType => {
        if (ruleType && !this.isEditMode) {
          const key = this.generateRuleKey(ruleType);
          this.ruleForm.patchValue({ ruleKey: key }, { emitEvent: false });
        }
      });
  }

  private jsonValidator(control: any): { [key: string]: any } | null {
    if (!control.value) return null;
    try {
      JSON.parse(control.value);
      return null;
    } catch {
      return { invalidJson: true };
    }
  }

  private generateRuleKey(ruleType: string): string {
    return ruleType.replace(/([A-Z])/g, '_$1').toLowerCase().substring(1);
  }

  private loadServices(): void {
    this.loading = true;
    this.error = null;

    const filter: BusinessRulesFilter = {
      sortBy: this.sortBy,
      sortDirection: this.sortDirection
    };

    this.businessRulesService.getServices(this.tenantId, filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (services) => {
          this.services = services;
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading services:', error);
          this.error = 'Failed to load services. Please try again.';
          this.loading = false;
        }
      });
  }

  applyFilters(): void {
    let filtered = [...this.services];

    // Search filter
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(service =>
        service.name.toLowerCase().includes(term) ||
        service.category.toLowerCase().includes(term) ||
        service.description?.toLowerCase().includes(term)
      );
    }

    // Category filter
    if (this.categoryFilter !== 'all') {
      filtered = filtered.filter(service => service.category === this.categoryFilter);
    }

    // Status filter
    if (this.statusFilter === 'active') {
      filtered = filtered.filter(service => service.isActive);
    } else if (this.statusFilter === 'inactive') {
      filtered = filtered.filter(service => !service.isActive);
    }

    this.filteredServices = filtered;
  }

  sort(field: 'name' | 'category' | 'ruleCount' | 'lastModified'): void {
    if (this.sortBy === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = field;
      this.sortDirection = 'asc';
    }
    this.loadServices();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.categoryFilter = 'all';
    this.statusFilter = 'all';
    this.applyFilters();
  }

  toggleServiceExpanded(service: Service): void {
    if (this.expandedServiceIds.has(service.id)) {
      this.expandedServiceIds.delete(service.id);
    } else {
      this.expandedServiceIds.add(service.id);
      this.loadServiceRules(service.id);
    }
  }

  isServiceExpanded(serviceId: number): boolean {
    return this.expandedServiceIds.has(serviceId);
  }

  private loadServiceRules(serviceId: number): void {
    if (this.serviceRules.has(serviceId)) return;

    this.loadingRules = true;
    this.businessRulesService.getServiceRules(this.tenantId, serviceId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rules) => {
          this.serviceRules.set(serviceId, rules);
          this.loadingRules = false;
        },
        error: (error) => {
          console.error('Error loading service rules:', error);
          this.loadingRules = false;
        }
      });
  }

  getServiceRules(serviceId: number): ServiceBusinessRule[] {
    return this.serviceRules.get(serviceId) || [];
  }

  openCreateRuleModal(service: Service): void {
    this.selectedService = service;
    this.selectedRule = null;
    this.isEditMode = false;
    this.ruleForm.reset({
      ruleType: '',
      ruleKey: '',
      ruleValue: '',
      validationMessage: '',
      priority: 3,
      isActive: true,
      minConfidenceScore: 0.8
    });
    this.modalService.open(this.ruleEditorModal, { size: 'lg', backdrop: 'static' });
  }

  openEditRuleModal(service: Service, rule: ServiceBusinessRule): void {
    this.selectedService = service;
    this.selectedRule = rule;
    this.isEditMode = true;
    this.ruleForm.patchValue({
      ruleType: rule.ruleType,
      ruleKey: rule.ruleKey,
      ruleValue: rule.ruleValue,
      validationMessage: rule.validationMessage,
      priority: rule.priority,
      isActive: rule.isActive,
      minConfidenceScore: rule.minConfidenceScore || 0.8
    });
    this.modalService.open(this.ruleEditorModal, { size: 'lg', backdrop: 'static' });
  }

  saveRule(): void {
    if (!this.ruleForm.valid || !this.selectedService) return;

    const formValue = this.ruleForm.value;

    if (this.isEditMode && this.selectedRule) {
      // Update existing rule
      const request: UpdateServiceRuleRequest = {
        ruleType: formValue.ruleType,
        ruleKey: formValue.ruleKey,
        ruleValue: formValue.ruleValue,
        validationMessage: formValue.validationMessage,
        priority: formValue.priority,
        isActive: formValue.isActive,
        minConfidenceScore: formValue.minConfidenceScore
      };

      this.businessRulesService.updateServiceRule(this.tenantId, this.selectedService.id, this.selectedRule.id, request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            this.serviceRules.delete(this.selectedService!.id);
            this.loadServiceRules(this.selectedService!.id);
            this.loadServices();
          },
          error: (error) => {
            console.error('Error updating rule:', error);
            alert('Failed to update rule. Please try again.');
          }
        });
    } else {
      // Create new rule
      const request: CreateServiceRuleRequest = {
        serviceId: this.selectedService.id,
        ruleType: formValue.ruleType,
        ruleKey: formValue.ruleKey,
        ruleValue: formValue.ruleValue,
        validationMessage: formValue.validationMessage,
        priority: formValue.priority,
        isActive: formValue.isActive,
        minConfidenceScore: formValue.minConfidenceScore
      };

      this.businessRulesService.createServiceRule(this.tenantId, this.selectedService.id, request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            this.serviceRules.delete(this.selectedService!.id);
            this.loadServiceRules(this.selectedService!.id);
            this.loadServices();
          },
          error: (error) => {
            console.error('Error creating rule:', error);
            alert('Failed to create rule. Please try again.');
          }
        });
    }
  }

  confirmDeleteRule(service: Service, rule: ServiceBusinessRule): void {
    this.ruleToDelete = {
      serviceId: service.id,
      ruleId: rule.id,
      ruleName: rule.ruleKey
    };
    this.modalService.open(this.deleteConfirmModal, { size: 'md', centered: true });
  }

  deleteRule(): void {
    if (!this.ruleToDelete) return;

    this.businessRulesService.deleteServiceRule(this.tenantId, this.ruleToDelete.serviceId, this.ruleToDelete.ruleId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.modalService.dismissAll();
          this.serviceRules.delete(this.ruleToDelete!.serviceId);
          this.loadServiceRules(this.ruleToDelete!.serviceId);
          this.loadServices();
          this.ruleToDelete = null;
        },
        error: (error) => {
          console.error('Error deleting rule:', error);
          alert('Failed to delete rule. Please try again.');
        }
      });
  }

  toggleRuleActive(service: Service, rule: ServiceBusinessRule): void {
    const newStatus = !rule.isActive;
    this.businessRulesService.toggleServiceRuleActive(this.tenantId, service.id, rule.id, newStatus)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          rule.isActive = newStatus;
        },
        error: (error) => {
          console.error('Error toggling rule:', error);
          alert('Failed to toggle rule status. Please try again.');
        }
      });
  }

  getRuleTypeLabel(ruleType: string): string {
    const type = this.ruleTypes.find(t => t.value === ruleType);
    return type ? type.label : ruleType;
  }

  getRuleTypeIcon(ruleType: string): string {
    const type = this.ruleTypes.find(t => t.value === ruleType);
    return type ? type.icon : 'settings';
  }

  getStatusBadgeClass(isActive: boolean): string {
    return isActive ? 'badge bg-success' : 'badge bg-secondary';
  }

  getPriorityStars(priority: number): string[] {
    return Array(5).fill('star').map((_, i) => i < priority ? 'star' : 'star-outline');
  }

  refresh(): void {
    this.loadServices();
  }
}
