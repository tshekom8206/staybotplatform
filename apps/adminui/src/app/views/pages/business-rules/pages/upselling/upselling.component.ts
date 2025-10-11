import { Component, OnInit, OnDestroy, AfterViewChecked, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbNavModule, NgbTooltipModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import { BusinessRulesService } from '../../services/business-rules.service';
import { UpsellItem, UpsellAnalytics } from '../../models/business-rules.models';
import * as feather from 'feather-icons';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-upselling',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgbNavModule,
    NgbTooltipModule,
    FeatherIconDirective,
    ReactiveFormsModule
  ],
  templateUrl: './upselling.component.html',
  styleUrl: './upselling.component.scss'
})
export class UpsellingComponent implements OnInit, OnDestroy, AfterViewChecked {
  private destroy$ = new Subject<void>();
  private businessRulesService = inject(BusinessRulesService);
  private modalService = inject(NgbModal);
  private fb = inject(FormBuilder);

  @ViewChild('upsellItemModal') upsellItemModal!: TemplateRef<any>;

  activeTab = 1;
  upsellItems: UpsellItem[] = [];
  analytics: UpsellAnalytics | null = null;
  loading = true;
  error: string | null = null;
  tenantId = 1;

  upsellForm!: FormGroup;
  isEditMode = false;
  editingItemId: number | null = null;

  ngOnInit(): void {
    this.initForm();
    this.loadUpsellData();
  }

  private initForm(): void {
    this.upsellForm = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', Validators.required],
      priceCents: [0, [Validators.required, Validators.min(0)]],
      unit: ['item', [Validators.required, Validators.maxLength(20)]],
      categories: this.fb.array([]),
      isActive: [true],
      leadTimeMinutes: [60, [Validators.required, Validators.min(0)]]
    });
  }

  get categories(): FormArray {
    return this.upsellForm.get('categories') as FormArray;
  }

  addCategory(category?: string): void {
    this.categories.push(this.fb.control(category || '', Validators.required));
  }

  removeCategory(index: number): void {
    this.categories.removeAt(index);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngAfterViewChecked(): void {
    feather.replace();
  }

  private loadUpsellData(): void {
    this.loading = true;
    this.error = null;

    this.businessRulesService.getUpsellItems(this.tenantId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (items) => {
          this.upsellItems = items;
          this.loadAnalytics();
        },
        error: (error) => {
          console.error('Error loading upsell items:', error);
          this.error = 'Failed to load upsell data. Please try again.';
          this.loading = false;
        }
      });
  }

  private loadAnalytics(): void {
    this.businessRulesService.getUpsellAnalytics(this.tenantId, 30)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (analytics) => {
          this.analytics = analytics;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading analytics:', error);
          this.loading = false;
        }
      });
  }

  formatCurrency(cents: number): string {
    return `R${(cents / 100).toFixed(2)}`;
  }

  formatPercentage(value: number): string {
    return `${value.toFixed(1)}%`;
  }

  // Helper methods for live previews in the modal
  getPricePreview(): string {
    const cents = this.upsellForm.get('priceCents')?.value || 0;
    if (cents === 0) return '';
    return `= ${this.formatCurrency(cents)}`;
  }

  getLeadTimePreview(): string {
    const minutes = this.upsellForm.get('leadTimeMinutes')?.value || 0;
    if (minutes === 0) return '= 0 minutes';

    const days = Math.floor(minutes / 1440);
    const hours = Math.floor((minutes % 1440) / 60);
    const mins = minutes % 60;

    const parts: string[] = [];
    if (days > 0) parts.push(`${days} ${days === 1 ? 'day' : 'days'}`);
    if (hours > 0) parts.push(`${hours} ${hours === 1 ? 'hour' : 'hours'}`);
    if (mins > 0) parts.push(`${mins} ${mins === 1 ? 'minute' : 'minutes'}`);

    return parts.length > 0 ? `= ${parts.join(' ')}` : '';
  }

  refresh(): void {
    this.loadUpsellData();
  }

  openCreateModal(): void {
    this.isEditMode = false;
    this.editingItemId = null;
    this.upsellForm.reset({
      title: '',
      description: '',
      priceCents: 0,
      unit: 'item',
      isActive: true,
      leadTimeMinutes: 60
    });
    this.categories.clear();
    this.addCategory(); // Start with one empty category
    this.modalService.open(this.upsellItemModal, { size: 'lg', backdrop: 'static' });
  }

  openEditModal(item: UpsellItem): void {
    this.isEditMode = true;
    this.editingItemId = item.id;
    this.categories.clear();

    // Populate form with item data
    this.upsellForm.patchValue({
      title: item.title,
      description: item.description,
      priceCents: item.priceCents,
      unit: item.unit,
      isActive: item.isActive,
      leadTimeMinutes: item.leadTimeMinutes
    });

    // Add categories
    item.categories.forEach(cat => this.addCategory(cat));

    this.modalService.open(this.upsellItemModal, { size: 'lg', backdrop: 'static' });
  }

  saveItem(): void {
    if (this.upsellForm.invalid) {
      Object.keys(this.upsellForm.controls).forEach(key => {
        const control = this.upsellForm.get(key);
        if (control?.invalid) {
          control.markAsTouched();
        }
      });
      return;
    }

    const formValue = this.upsellForm.value;
    const itemData = {
      ...formValue,
      categories: formValue.categories.filter((c: string) => c.trim() !== '')
    };

    if (this.isEditMode && this.editingItemId) {
      // Update existing item
      this.businessRulesService.updateUpsellItem(this.tenantId, this.editingItemId, itemData)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            Swal.fire('Success', 'Upsell item updated successfully', 'success');
            this.loadUpsellData();
          },
          error: (error) => {
            console.error('Error updating upsell item:', error);
            Swal.fire('Error', 'Failed to update upsell item', 'error');
          }
        });
    } else {
      // Create new item
      this.businessRulesService.createUpsellItem(this.tenantId, itemData)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            Swal.fire('Success', 'Upsell item created successfully', 'success');
            this.loadUpsellData();
          },
          error: (error) => {
            console.error('Error creating upsell item:', error);
            Swal.fire('Error', 'Failed to create upsell item', 'error');
          }
        });
    }
  }

  deleteItem(item: UpsellItem): void {
    Swal.fire({
      title: 'Delete Upsell Item?',
      text: `Are you sure you want to delete "${item.title}"? This action cannot be undone.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Yes, delete it',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        this.businessRulesService.deleteUpsellItem(this.tenantId, item.id)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              Swal.fire('Deleted!', 'Upsell item has been deleted.', 'success');
              this.loadUpsellData();
            },
            error: (error) => {
              console.error('Error deleting upsell item:', error);
              Swal.fire('Error', 'Failed to delete upsell item', 'error');
            }
          });
      }
    });
  }

  closeModal(): void {
    this.modalService.dismissAll();
  }
}
