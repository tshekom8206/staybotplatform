import { Component, OnInit, OnDestroy, AfterViewChecked, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbModal, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import { BusinessRulesService } from '../../services/business-rules.service';
import { WeatherUpsellRule, WeatherConditionInfo, ServiceForUpsell } from '../../models/business-rules.models';
import * as feather from 'feather-icons';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-weather-upselling',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgbTooltipModule,
    FeatherIconDirective,
    ReactiveFormsModule
  ],
  templateUrl: './weather-upselling.component.html',
  styleUrl: './weather-upselling.component.scss'
})
export class WeatherUpsellingComponent implements OnInit, OnDestroy, AfterViewChecked {
  private destroy$ = new Subject<void>();
  private businessRulesService = inject(BusinessRulesService);
  private modalService = inject(NgbModal);
  private fb = inject(FormBuilder);

  @ViewChild('ruleModal') ruleModal!: TemplateRef<any>;

  rules: WeatherUpsellRule[] = [];
  weatherConditions: WeatherConditionInfo[] = [];
  availableServices: ServiceForUpsell[] = [];
  selectedServiceIds: number[] = [];
  loading = true;
  error: string | null = null;
  tenantId = 1;

  ruleForm!: FormGroup;
  isEditMode = false;
  editingRuleId: number | null = null;

  // Icon options for banner
  iconOptions = [
    { value: 'sun', label: 'Sun', icon: 'bi-sun' },
    { value: 'cloud-sun', label: 'Partly Cloudy', icon: 'bi-cloud-sun' },
    { value: 'cloud', label: 'Cloudy', icon: 'bi-cloud' },
    { value: 'cloud-rain', label: 'Rain', icon: 'bi-cloud-rain' },
    { value: 'cloud-lightning-rain', label: 'Thunderstorm', icon: 'bi-cloud-lightning-rain' },
    { value: 'snow', label: 'Snow', icon: 'bi-snow' },
    { value: 'thermometer-sun', label: 'Hot', icon: 'bi-thermometer-sun' },
    { value: 'thermometer-snow', label: 'Cold', icon: 'bi-thermometer-snow' },
    { value: 'water', label: 'Pool', icon: 'bi-water' },
    { value: 'cup-hot', label: 'Hot Drinks', icon: 'bi-cup-hot' },
    { value: 'flower1', label: 'Spa', icon: 'bi-flower1' }
  ];

  ngOnInit(): void {
    this.initForm();
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngAfterViewChecked(): void {
    feather.replace();
  }

  private initForm(): void {
    this.ruleForm = this.fb.group({
      weatherCondition: ['', Validators.required],
      minTemperature: [null],
      maxTemperature: [null],
      bannerText: ['', [Validators.required, Validators.maxLength(200)]],
      bannerIcon: ['sun', Validators.required],
      priority: [0, [Validators.required, Validators.min(0)]],
      isActive: [true]
    });
  }

  private loadData(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      rules: this.businessRulesService.getWeatherUpsellRules(this.tenantId),
      conditions: this.businessRulesService.getWeatherConditions(),
      services: this.businessRulesService.getAvailableServicesForWeatherUpsell(this.tenantId)
    })
    .pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        this.rules = data.rules;
        this.weatherConditions = data.conditions;
        this.availableServices = data.services;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading weather upselling data:', error);
        this.error = 'Failed to load weather upselling data. Please try again.';
        this.loading = false;
      }
    });
  }

  refresh(): void {
    this.loadData();
  }

  openCreateModal(): void {
    this.isEditMode = false;
    this.editingRuleId = null;
    this.selectedServiceIds = [];
    this.ruleForm.reset({
      weatherCondition: '',
      minTemperature: null,
      maxTemperature: null,
      bannerText: '',
      bannerIcon: 'sun',
      priority: 0,
      isActive: true
    });
    this.modalService.open(this.ruleModal, { size: 'lg', backdrop: 'static' });
  }

  openEditModal(rule: WeatherUpsellRule): void {
    this.isEditMode = true;
    this.editingRuleId = rule.id;

    // Parse service IDs from JSON
    try {
      this.selectedServiceIds = JSON.parse(rule.serviceIds) || [];
    } catch {
      this.selectedServiceIds = [];
    }

    this.ruleForm.patchValue({
      weatherCondition: rule.weatherCondition,
      minTemperature: rule.minTemperature,
      maxTemperature: rule.maxTemperature,
      bannerText: rule.bannerText,
      bannerIcon: rule.bannerIcon || 'sun',
      priority: rule.priority,
      isActive: rule.isActive
    });

    this.modalService.open(this.ruleModal, { size: 'lg', backdrop: 'static' });
  }

  toggleServiceSelection(serviceId: number): void {
    const index = this.selectedServiceIds.indexOf(serviceId);
    if (index > -1) {
      this.selectedServiceIds.splice(index, 1);
    } else {
      this.selectedServiceIds.push(serviceId);
    }
  }

  isServiceSelected(serviceId: number): boolean {
    return this.selectedServiceIds.includes(serviceId);
  }

  onConditionChange(): void {
    const condition = this.weatherConditions.find(
      c => c.code === this.ruleForm.get('weatherCondition')?.value
    );
    if (condition) {
      this.ruleForm.patchValue({
        minTemperature: condition.defaultMinTemp,
        maxTemperature: condition.defaultMaxTemp,
        bannerIcon: condition.defaultIcon
      });
    }
  }

  saveRule(): void {
    if (this.ruleForm.invalid) {
      Object.keys(this.ruleForm.controls).forEach(key => {
        const control = this.ruleForm.get(key);
        if (control?.invalid) {
          control.markAsTouched();
        }
      });
      return;
    }

    if (this.selectedServiceIds.length === 0) {
      Swal.fire('Error', 'Please select at least one service to promote', 'error');
      return;
    }

    const formValue = this.ruleForm.value;
    const condition = this.weatherConditions.find(c => c.code === formValue.weatherCondition);

    const request = {
      weatherCondition: formValue.weatherCondition,
      minTemperature: formValue.minTemperature,
      maxTemperature: formValue.maxTemperature,
      weatherCodes: condition?.wmoCodes ? JSON.stringify(condition.wmoCodes) : undefined,
      serviceIds: JSON.stringify(this.selectedServiceIds),
      bannerText: formValue.bannerText,
      bannerIcon: formValue.bannerIcon,
      priority: formValue.priority,
      isActive: formValue.isActive
    };

    if (this.isEditMode && this.editingRuleId) {
      this.businessRulesService.updateWeatherUpsellRule(this.tenantId, this.editingRuleId, request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            Swal.fire('Success', 'Weather upsell rule updated successfully', 'success');
            this.loadData();
          },
          error: (error) => {
            console.error('Error updating rule:', error);
            Swal.fire('Error', 'Failed to update rule', 'error');
          }
        });
    } else {
      this.businessRulesService.createWeatherUpsellRule(this.tenantId, request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            Swal.fire('Success', 'Weather upsell rule created successfully', 'success');
            this.loadData();
          },
          error: (error) => {
            console.error('Error creating rule:', error);
            Swal.fire('Error', 'Failed to create rule', 'error');
          }
        });
    }
  }

  toggleRule(rule: WeatherUpsellRule): void {
    this.businessRulesService.toggleWeatherUpsellRule(this.tenantId, rule.id, !rule.isActive)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          rule.isActive = !rule.isActive;
        },
        error: (error) => {
          console.error('Error toggling rule:', error);
          Swal.fire('Error', 'Failed to toggle rule status', 'error');
        }
      });
  }

  deleteRule(rule: WeatherUpsellRule): void {
    Swal.fire({
      title: 'Delete Rule?',
      text: `Are you sure you want to delete the "${rule.weatherCondition}" weather rule? This action cannot be undone.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Yes, delete it',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        this.businessRulesService.deleteWeatherUpsellRule(this.tenantId, rule.id)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              Swal.fire('Deleted!', 'Weather upsell rule has been deleted.', 'success');
              this.loadData();
            },
            error: (error) => {
              console.error('Error deleting rule:', error);
              Swal.fire('Error', 'Failed to delete rule', 'error');
            }
          });
      }
    });
  }

  closeModal(): void {
    this.modalService.dismissAll();
  }

  getConditionName(code: string): string {
    const condition = this.weatherConditions.find(c => c.code === code);
    return condition?.name || code;
  }

  getServiceNames(serviceIds: string): string {
    try {
      const ids: number[] = JSON.parse(serviceIds);
      const names = ids
        .map(id => this.availableServices.find(s => s.id === id)?.name)
        .filter(name => name);
      return names.length > 0 ? names.join(', ') : 'No services selected';
    } catch {
      return 'No services selected';
    }
  }

  getTemperatureRange(rule: WeatherUpsellRule): string {
    if (rule.minTemperature !== null && rule.maxTemperature !== null) {
      return `${rule.minTemperature}°C - ${rule.maxTemperature}°C`;
    } else if (rule.minTemperature !== null) {
      return `> ${rule.minTemperature}°C`;
    } else if (rule.maxTemperature !== null) {
      return `< ${rule.maxTemperature}°C`;
    }
    return 'Any';
  }
}
