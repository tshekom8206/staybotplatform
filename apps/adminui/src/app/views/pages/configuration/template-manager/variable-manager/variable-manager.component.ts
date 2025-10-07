import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbTooltipModule, NgbAlertModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import {
  TemplateService,
  ResponseVariable,
  CreateVariableRequest
} from '../../../../../core/services/template.service';

@Component({
  selector: 'app-variable-manager',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    NgbTooltipModule,
    NgbAlertModule,
    FeatherIconDirective
  ],
  templateUrl: './variable-manager.component.html',
  styleUrl: './variable-manager.component.scss'
})
export class VariableManagerComponent implements OnInit, OnDestroy {
  @Input() variables: ResponseVariable[] = [];
  @Input() categories: string[] = [];

  @Output() variablesSaved = new EventEmitter<void>();

  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private templateService = inject(TemplateService);

  variableForm: FormGroup;
  editingVariable: ResponseVariable | null = null;

  // UI State
  loading = false;
  saving = false;
  error: string | null = null;
  success: string | null = null;
  showForm = false;

  // Filtered and organized data
  filteredVariables: ResponseVariable[] = [];
  variablesByCategory: { [category: string]: ResponseVariable[] } = {};

  // Filter state
  selectedCategory = '';
  searchTerm = '';
  showActiveOnly = true;

  // Available predefined variables
  availableVariableNames = this.templateService.getAvailableVariableNames();

  constructor() {
    this.initializeForm();
  }

  ngOnInit(): void {
    this.organizeVariables();
    this.applyFilters();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.variableForm = this.fb.group({
      variableName: ['', [Validators.required, Validators.maxLength(50)]],
      variableValue: ['', [Validators.required]],
      category: [''],
      isActive: [true]
    });
  }

  private organizeVariables(): void {
    this.variablesByCategory = {};

    this.variables.forEach(variable => {
      const category = variable.category || 'Uncategorized';
      if (!this.variablesByCategory[category]) {
        this.variablesByCategory[category] = [];
      }
      this.variablesByCategory[category].push(variable);
    });

    // Sort variables within each category
    Object.keys(this.variablesByCategory).forEach(category => {
      this.variablesByCategory[category].sort((a, b) =>
        a.variableName.localeCompare(b.variableName)
      );
    });
  }

  private applyFilters(): void {
    let filtered = [...this.variables];

    // Apply category filter
    if (this.selectedCategory) {
      filtered = filtered.filter(v => v.category === this.selectedCategory);
    }

    // Apply search filter
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(v =>
        v.variableName.toLowerCase().includes(term) ||
        v.variableValue.toLowerCase().includes(term) ||
        (v.category && v.category.toLowerCase().includes(term))
      );
    }

    // Apply active filter
    if (this.showActiveOnly) {
      filtered = filtered.filter(v => v.isActive);
    }

    this.filteredVariables = filtered.sort((a, b) => {
      // Sort by category first, then by variable name
      if (a.category !== b.category) {
        return (a.category || '').localeCompare(b.category || '');
      }
      return a.variableName.localeCompare(b.variableName);
    });
  }

  onCategoryFilterChange(category: string): void {
    this.selectedCategory = category;
    this.applyFilters();
  }

  onSearchChange(): void {
    this.applyFilters();
  }

  onActiveFilterChange(): void {
    this.applyFilters();
  }

  onClearFilters(): void {
    this.selectedCategory = '';
    this.searchTerm = '';
    this.showActiveOnly = true;
    this.applyFilters();
  }

  onShowForm(): void {
    this.editingVariable = null;
    this.variableForm.reset({
      variableName: '',
      variableValue: '',
      category: '',
      isActive: true
    });
    this.showForm = true;
  }

  onEditVariable(variable: ResponseVariable): void {
    this.editingVariable = variable;
    this.variableForm.patchValue({
      variableName: variable.variableName,
      variableValue: variable.variableValue,
      category: variable.category || '',
      isActive: variable.isActive
    });
    this.showForm = true;
  }

  onCancelEdit(): void {
    this.showForm = false;
    this.editingVariable = null;
    this.variableForm.reset();
  }

  onSaveVariable(): void {
    if (this.variableForm.invalid) {
      this.markFormGroupTouched(this.variableForm);
      this.error = 'Please correct the errors in the form.';
      setTimeout(() => this.error = null, 5000);
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.variableForm.value;
    const variableRequest: CreateVariableRequest = {
      variableName: formValue.variableName,
      variableValue: formValue.variableValue,
      category: formValue.category || undefined,
      isActive: formValue.isActive
    };

    this.templateService.createOrUpdateVariable(variableRequest)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.saving = false;
          this.success = this.editingVariable ?
            'Variable updated successfully!' :
            'Variable created successfully!';

          this.showForm = false;
          this.editingVariable = null;
          this.variablesSaved.emit();

          setTimeout(() => this.success = null, 3000);
        },
        error: (error) => {
          console.error('Error saving variable:', error);
          this.saving = false;
          this.error = 'Failed to save variable. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  onDeleteVariable(variable: ResponseVariable): void {
    if (confirm(`Are you sure you want to delete the variable "${variable.variableName}"? This action cannot be undone.`)) {
      this.templateService.deleteVariable(variable.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.success = 'Variable deleted successfully!';
            this.variablesSaved.emit();
            setTimeout(() => this.success = null, 3000);
          },
          error: (error) => {
            console.error('Error deleting variable:', error);
            this.error = 'Failed to delete variable. Please try again.';
            setTimeout(() => this.error = null, 5000);
          }
        });
    }
  }

  onSelectPredefinedVariable(event: Event): void {
    const select = event.target as HTMLSelectElement;
    if (select.value) {
      this.variableForm.patchValue({
        variableName: select.value,
        category: this.getCategoryForPredefinedVariable(select.value)
      });
      select.selectedIndex = 0; // Reset dropdown
    }
  }

  private getCategoryForPredefinedVariable(variableName: string): string {
    for (const [category, variables] of Object.entries(this.availableVariableNames)) {
      if (variables.includes(variableName)) {
        return category;
      }
    }
    return '';
  }

  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(field => {
      const control = formGroup.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  // Form validation helpers
  isFieldInvalid(fieldName: string): boolean {
    const field = this.variableForm.get(fieldName);
    return !!(field && field.invalid && field.touched);
  }

  getFieldError(fieldName: string): string {
    const field = this.variableForm.get(fieldName);
    if (field && field.errors && field.touched) {
      if (field.errors['required']) {
        return `${fieldName.charAt(0).toUpperCase() + fieldName.slice(1)} is required`;
      }
      if (field.errors['maxlength']) {
        return `${fieldName.charAt(0).toUpperCase() + fieldName.slice(1)} is too long`;
      }
    }
    return '';
  }

  get availableCategories(): string[] {
    const existingCategories = Array.from(new Set(this.variables.map(v => v.category).filter((c): c is string => !!c)));
    const predefinedCategories = Object.keys(this.availableVariableNames);
    return Array.from(new Set([...existingCategories, ...predefinedCategories, ...this.categories]));
  }

  get categoryKeys(): string[] {
    return Object.keys(this.variablesByCategory).sort();
  }

  get hasFilters(): boolean {
    return !!(this.selectedCategory || this.searchTerm || !this.showActiveOnly);
  }

  get filteredVariableCount(): number {
    return this.filteredVariables.length;
  }

  get totalVariableCount(): number {
    return this.variables.length;
  }

  get isEditMode(): boolean {
    return !!this.editingVariable;
  }

  get formTitle(): string {
    return this.isEditMode ? 'Edit Variable' : 'Create Variable';
  }

  get saveButtonText(): string {
    if (this.saving) {
      return this.isEditMode ? 'Updating...' : 'Creating...';
    }
    return this.isEditMode ? 'Update Variable' : 'Create Variable';
  }

  trackByVariableId(index: number, variable: ResponseVariable): number {
    return variable.id;
  }

  // Helper for template
  Object = Object;
}