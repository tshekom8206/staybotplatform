import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  ServicesService,
  HotelService,
  ServiceCategory,
  ServiceIcon,
  CreateServiceRequest,
  UpdateServiceRequest
} from '../../../../core/services/services.service';

@Component({
  selector: 'app-services',
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
  templateUrl: './services.component.html',
  styleUrl: './services.component.scss'
})
export class ServicesComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private servicesService = inject(ServicesService);
  private modalService = inject(NgbModal);

  @ViewChild('serviceModal') serviceModal!: TemplateRef<any>;

  // Data properties
  services: HotelService[] = [];
  filteredServices: HotelService[] = [];
  loading = false;
  saving = false;
  error: string | null = null;
  success: string | null = null;

  // Modal properties
  selectedService: HotelService | null = null;
  isEditMode = false;
  serviceForm!: FormGroup;

  // Filter and search properties
  searchTerm = '';
  categoryFilter = 'all';
  availabilityFilter = 'all';

  // Configuration data
  serviceCategories: ServiceCategory[] = [];
  serviceIcons: ServiceIcon[] = [];
  contactMethods: string[] = [];
  pricingUnits: string[] = [];
  currencies: Array<{code: string, name: string}> = [];

  ngOnInit(): void {
    this.initializeForm();
    this.loadConfigurationData();
    this.loadServices();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.serviceForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      description: ['', [Validators.maxLength(500)]],
      category: ['', Validators.required],
      icon: [''],
      isAvailable: [true],
      isChargeable: [false],
      price: [null],
      currency: ['USD'],
      pricingUnit: [''],
      availableHours: [''],
      contactMethod: [''],
      contactInfo: [''],
      priority: [0, [Validators.min(0)]],
      specialInstructions: ['', [Validators.maxLength(1000)]],
      imageUrl: ['', [Validators.maxLength(200)]],
      requiresAdvanceBooking: [false],
      advanceBookingHours: [null]
    });

    // Watch chargeable changes
    this.serviceForm.get('isChargeable')?.valueChanges.subscribe(isChargeable => {
      const priceControl = this.serviceForm.get('price');
      const currencyControl = this.serviceForm.get('currency');
      const pricingUnitControl = this.serviceForm.get('pricingUnit');

      if (isChargeable) {
        priceControl?.setValidators([Validators.required, Validators.min(0)]);
        currencyControl?.setValidators([Validators.required]);
        pricingUnitControl?.setValidators([Validators.required]);
      } else {
        priceControl?.clearValidators();
        currencyControl?.clearValidators();
        pricingUnitControl?.clearValidators();
      }

      priceControl?.updateValueAndValidity();
      currencyControl?.updateValueAndValidity();
      pricingUnitControl?.updateValueAndValidity();
    });

    // Watch advance booking changes
    this.serviceForm.get('requiresAdvanceBooking')?.valueChanges.subscribe(requiresBooking => {
      const hoursControl = this.serviceForm.get('advanceBookingHours');

      if (requiresBooking) {
        hoursControl?.setValidators([Validators.required, Validators.min(1)]);
      } else {
        hoursControl?.clearValidators();
      }

      hoursControl?.updateValueAndValidity();
    });
  }

  private loadConfigurationData(): void {
    forkJoin({
      categories: this.servicesService.getServiceCategories(),
      icons: this.servicesService.getServiceIcons(),
      currencies: this.servicesService.getCurrencies()
    })
    .pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        this.serviceCategories = data.categories;
        this.serviceIcons = data.icons;
        this.currencies = data.currencies;
        this.contactMethods = this.servicesService.getContactMethods();
        this.pricingUnits = this.servicesService.getPricingUnits();
      },
      error: (error) => {
        console.error('Error loading configuration data:', error);
        this.error = 'Failed to load configuration data. Please refresh the page.';
      }
    });
  }

  private loadServices(): void {
    this.loading = true;
    this.error = null;

    this.servicesService.getServices()
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

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(service =>
        service.name.toLowerCase().includes(term) ||
        service.description?.toLowerCase().includes(term) ||
        service.category.toLowerCase().includes(term)
      );
    }

    // Category filter
    if (this.categoryFilter !== 'all') {
      filtered = filtered.filter(service => service.category === this.categoryFilter);
    }

    // Availability filter
    if (this.availabilityFilter !== 'all') {
      filtered = filtered.filter(service =>
        this.availabilityFilter === 'available' ? service.isAvailable : !service.isAvailable
      );
    }

    this.filteredServices = filtered.sort((a, b) => a.priority - b.priority || a.name.localeCompare(b.name));
  }

  openCreateModal(): void {
    this.isEditMode = false;
    this.selectedService = null;
    this.serviceForm.reset({
      isAvailable: true,
      isChargeable: false,
      currency: 'USD',
      priority: 0,
      requiresAdvanceBooking: false
    });
    this.modalService.open(this.serviceModal, { size: 'lg', backdrop: 'static' });
  }

  openEditModal(service: HotelService): void {
    this.isEditMode = true;
    this.selectedService = service;
    this.serviceForm.patchValue({
      name: service.name,
      description: service.description,
      category: service.category,
      icon: service.icon,
      isAvailable: service.isAvailable,
      isChargeable: service.isChargeable,
      price: service.price,
      currency: service.currency,
      pricingUnit: service.pricingUnit,
      availableHours: service.availableHours,
      contactMethod: service.contactMethod,
      contactInfo: service.contactInfo,
      priority: service.priority,
      specialInstructions: service.specialInstructions,
      imageUrl: service.imageUrl,
      requiresAdvanceBooking: service.requiresAdvanceBooking,
      advanceBookingHours: service.advanceBookingHours
    });
    this.modalService.open(this.serviceModal, { size: 'lg', backdrop: 'static' });
  }

  saveService(): void {
    if (this.serviceForm.invalid) {
      this.markFormGroupTouched();
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.serviceForm.value;
    const serviceData: CreateServiceRequest | UpdateServiceRequest = {
      name: formValue.name,
      description: formValue.description,
      category: formValue.category,
      icon: formValue.icon,
      isAvailable: formValue.isAvailable,
      isChargeable: formValue.isChargeable,
      price: formValue.isChargeable ? formValue.price : null,
      currency: formValue.isChargeable ? formValue.currency : null,
      pricingUnit: formValue.isChargeable ? formValue.pricingUnit : null,
      availableHours: formValue.availableHours,
      contactMethod: formValue.contactMethod,
      contactInfo: formValue.contactInfo,
      priority: formValue.priority,
      specialInstructions: formValue.specialInstructions,
      imageUrl: formValue.imageUrl,
      requiresAdvanceBooking: formValue.requiresAdvanceBooking,
      advanceBookingHours: formValue.requiresAdvanceBooking ? formValue.advanceBookingHours : null
    };

    const operation = this.isEditMode
      ? this.servicesService.updateService(this.selectedService!.id, serviceData)
      : this.servicesService.createService(serviceData);

    operation
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (service) => {
          if (this.isEditMode) {
            const index = this.services.findIndex(s => s.id === service.id);
            if (index !== -1) {
              this.services[index] = service;
            }
          } else {
            this.services.push(service);
          }

          this.applyFilters();
          this.saving = false;
          this.success = `Service ${this.isEditMode ? 'updated' : 'created'} successfully!`;
          this.modalService.dismissAll();

          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving service:', error);

          if (error.status === 401) {
            this.error = 'Your session has expired. Please log in again.';
          } else if (error.status === 403) {
            this.error = 'You do not have permission to manage services.';
          } else if (error.status === 400) {
            this.error = 'Invalid data provided. Please check your input and try again.';
          } else {
            this.error = `Failed to ${this.isEditMode ? 'update' : 'create'} service. Please try again.`;
          }

          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  deleteService(service: HotelService): void {
    if (!confirm(`Are you sure you want to delete "${service.name}"? This action cannot be undone.`)) {
      return;
    }

    this.servicesService.deleteService(service.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.services = this.services.filter(s => s.id !== service.id);
          this.applyFilters();
          this.success = 'Service deleted successfully!';
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error deleting service:', error);
          this.error = 'Failed to delete service. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  private markFormGroupTouched(): void {
    Object.keys(this.serviceForm.controls).forEach(field => {
      const control = this.serviceForm.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  getCategoryLabel(categoryValue: string): string {
    const category = this.serviceCategories.find(c => c.value === categoryValue);
    return category ? category.label : categoryValue;
  }

  getIconLabel(iconName: string): string {
    const icon = this.serviceIcons.find(i => i.name === iconName);
    return icon ? icon.label || icon.name : iconName;
  }

  get uniqueCategories(): string[] {
    return [...new Set(this.services.map(s => s.category))];
  }
}