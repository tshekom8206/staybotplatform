import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbTooltipModule, NgbAlertModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import {
  TemplateService,
  ResponseTemplate,
  ResponseVariable,
  CreateTemplateRequest,
  UpdateTemplateRequest,
  ProcessedTemplate
} from '../../../../../core/services/template.service';

@Component({
  selector: 'app-template-editor',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbTooltipModule,
    NgbAlertModule,
    FeatherIconDirective
  ],
  templateUrl: './template-editor.component.html',
  styleUrl: './template-editor.component.scss'
})
export class TemplateEditorComponent implements OnInit, OnDestroy {
  @Input() template: ResponseTemplate | null = null;
  @Input() variables: ResponseVariable[] = [];
  @Input() categories: string[] = [];
  @Input() availableLanguages: { code: string; name: string }[] = [];

  @Output() back = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private templateService = inject(TemplateService);

  templateForm: FormGroup;
  previewContent: ProcessedTemplate | null = null;

  loading = false;
  saving = false;
  error: string | null = null;
  success: string | null = null;

  // Editor state
  showPreview = false;
  showVariableHelper = false;
  cursorPosition = 0;

  // Available template keys and variables
  availableTemplateKeys = this.templateService.getAvailableTemplateKeys();
  availableVariableNames = this.templateService.getAvailableVariableNames();
  variablesByCategory: { [category: string]: ResponseVariable[] } = {};

  constructor() {
    this.initializeForm();
  }

  ngOnInit(): void {
    this.organizeVariablesByCategory();
    this.populateForm();
    this.setupFormSubscriptions();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.templateForm = this.fb.group({
      templateKey: ['', [Validators.required, Validators.maxLength(100)]],
      category: ['', [Validators.required, Validators.maxLength(50)]],
      language: ['en', [Validators.required, Validators.maxLength(10)]],
      template: ['', [Validators.required]],
      isActive: [true],
      priority: [1, [Validators.required, Validators.min(1), Validators.max(10)]]
    });
  }

  private setupFormSubscriptions(): void {
    // Update preview when template content changes
    this.templateForm.get('template')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.updatePreview();
      });
  }

  private organizeVariablesByCategory(): void {
    this.variablesByCategory = {};
    this.variables.forEach(variable => {
      const category = variable.category || 'Uncategorized';
      if (!this.variablesByCategory[category]) {
        this.variablesByCategory[category] = [];
      }
      this.variablesByCategory[category].push(variable);
    });
  }

  private populateForm(): void {
    if (this.template) {
      this.templateForm.patchValue({
        templateKey: this.template.templateKey,
        category: this.template.category,
        language: this.template.language,
        template: this.template.template,
        isActive: this.template.isActive,
        priority: this.template.priority
      });
    }
    this.updatePreview();
  }

  private updatePreview(): void {
    const templateContent = this.templateForm.get('template')?.value || '';
    const variableMap: { [key: string]: string } = {};

    // Create variable map for preview
    this.variables.forEach(variable => {
      variableMap[variable.variableName] = variable.variableValue;
    });

    this.previewContent = this.templateService.previewTemplate(templateContent, variableMap);
  }


  onInsertVariable(variableName: string): void {
    const templateControl = this.templateForm.get('template');
    if (templateControl) {
      const currentValue = templateControl.value || '';
      const textArea = document.getElementById('templateContent') as HTMLTextAreaElement;

      if (textArea) {
        const start = textArea.selectionStart;
        const end = textArea.selectionEnd;
        const variableText = `{{${variableName}}}`;

        const newValue = currentValue.substring(0, start) + variableText + currentValue.substring(end);
        templateControl.setValue(newValue);

        // Set cursor position after the inserted variable
        setTimeout(() => {
          textArea.setSelectionRange(start + variableText.length, start + variableText.length);
          textArea.focus();
        }, 0);
      }
    }

    this.showVariableHelper = false;
  }

  onTogglePreview(): void {
    this.showPreview = !this.showPreview;
    this.updatePreview();
  }

  onToggleVariableHelper(): void {
    this.showVariableHelper = !this.showVariableHelper;
  }

  onSave(): void {
    if (this.templateForm.invalid) {
      this.markFormGroupTouched(this.templateForm);
      this.error = 'Please correct the errors in the form before saving.';
      setTimeout(() => this.error = null, 5000);
      return;
    }

    this.saving = true;
    this.error = null;
    this.success = null;

    const formValue = this.templateForm.value;

    if (this.template) {
      // Update existing template
      const updateRequest: UpdateTemplateRequest = {
        templateKey: formValue.templateKey,
        category: formValue.category,
        language: formValue.language,
        template: formValue.template,
        isActive: formValue.isActive,
        priority: formValue.priority
      };

      this.templateService.updateTemplate(this.template.id, updateRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.saving = false;
            this.success = 'Template updated successfully!';
            setTimeout(() => this.saved.emit(), 1500);
          },
          error: (error) => {
            console.error('Error updating template:', error);
            this.handleSaveError(error);
          }
        });
    } else {
      // Create new template
      const createRequest: CreateTemplateRequest = {
        templateKey: formValue.templateKey,
        category: formValue.category,
        language: formValue.language,
        template: formValue.template,
        isActive: formValue.isActive,
        priority: formValue.priority
      };

      this.templateService.createTemplate(createRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.saving = false;
            this.success = 'Template created successfully!';
            setTimeout(() => this.saved.emit(), 1500);
          },
          error: (error) => {
            console.error('Error creating template:', error);
            this.handleSaveError(error);
          }
        });
    }
  }

  private handleSaveError(error: any): void {
    this.saving = false;

    if (error.status === 400 && error.error?.error?.includes('already exists')) {
      this.error = 'A template with this key and language combination already exists.';
    } else if (error.status === 401) {
      this.error = 'Your session has expired. Please log in again.';
    } else if (error.status === 403) {
      this.error = 'You do not have permission to save templates.';
    } else {
      this.error = 'Failed to save template. Please try again.';
    }

    setTimeout(() => this.error = null, 10000);
  }

  onCancel(): void {
    if (this.templateForm.dirty) {
      if (confirm('You have unsaved changes. Are you sure you want to cancel?')) {
        this.back.emit();
      }
    } else {
      this.back.emit();
    }
  }

  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(field => {
      const control = formGroup.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  // Form validation helpers
  isFieldInvalid(fieldName: string): boolean {
    const field = this.templateForm.get(fieldName);
    return !!(field && field.invalid && field.touched);
  }

  getFieldError(fieldName: string): string {
    const field = this.templateForm.get(fieldName);
    if (field && field.errors && field.touched) {
      if (field.errors['required']) {
        return `${fieldName.charAt(0).toUpperCase() + fieldName.slice(1)} is required`;
      }
      if (field.errors['maxlength']) {
        return `${fieldName.charAt(0).toUpperCase() + fieldName.slice(1)} is too long`;
      }
      if (field.errors['min']) {
        return `${fieldName.charAt(0).toUpperCase() + fieldName.slice(1)} must be at least ${field.errors['min'].min}`;
      }
      if (field.errors['max']) {
        return `${fieldName.charAt(0).toUpperCase() + fieldName.slice(1)} must be at most ${field.errors['max'].max}`;
      }
    }
    return '';
  }

  get isEditMode(): boolean {
    return !!this.template;
  }

  get pageTitle(): string {
    return this.isEditMode ? 'Edit Template' : 'Create Template';
  }

  get saveButtonText(): string {
    if (this.saving) {
      return this.isEditMode ? 'Updating...' : 'Creating...';
    }
    return this.isEditMode ? 'Update Template' : 'Create Template';
  }

  get categoryList(): string[] {
    return this.categories.length > 0 ? this.categories : this.templateService.getDefaultCategories();
  }

  get variableCategoryKeys(): string[] {
    return Object.keys(this.variablesByCategory);
  }

  get hasVariables(): boolean {
    return this.variables.length > 0;
  }

  get hasMissingVariables(): boolean {
    return (this.previewContent?.missingVariables?.length ?? 0) > 0;
  }

  // Helper for template
  Object = Object;

  // Template example strings for display
  variableExampleText = '{{variable_name}}';
  examplePlaceholder = `✍️ Start typing your template content here...

💡 Pro tip: Use double curly braces to insert dynamic content like:
• {{guest_name}} - Guest's name
• {{room_number}} - Room number
• {{hotel_name}} - Hotel name

Example:
Hello {{guest_name}}, welcome to {{hotel_name}}! Your room {{room_number}} is ready.`;

  onTemplateKeySelect(event: Event): void {
    const select = event.target as HTMLSelectElement;
    if (select.value) {
      this.templateForm.patchValue({ templateKey: select.value });
      select.selectedIndex = 0; // Reset dropdown
    }
  }

  onCategorySelect(event: Event): void {
    const select = event.target as HTMLSelectElement;
    if (select.value) {
      this.templateForm.patchValue({ category: select.value });
      select.selectedIndex = 0; // Reset dropdown
    }
  }
}