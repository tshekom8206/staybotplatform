import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, forkJoin, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { NgbTooltipModule, NgbAlertModule, NgbModal, NgbPaginationModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  TemplateService,
  ResponseTemplate,
  ResponseVariable,
  TemplateSearchParams,
  TemplateListResponse,
  CreateTemplateRequest,
  UpdateTemplateRequest
} from '../../../../core/services/template.service';
import { TemplateEditorComponent } from './template-editor/template-editor.component';
import { VariableManagerComponent } from './variable-manager/variable-manager.component';

@Component({
  selector: 'app-template-manager',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbPaginationModule,
    FeatherIconDirective,
    TemplateEditorComponent,
    VariableManagerComponent
  ],
  templateUrl: './template-manager.component.html',
  styleUrl: './template-manager.component.scss'
})
export class TemplateManagerComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private templateService = inject(TemplateService);
  private modalService = inject(NgbModal);

  // Forms
  searchForm: FormGroup;

  // Data
  templates: ResponseTemplate[] = [];
  variables: ResponseVariable[] = [];
  categories: string[] = [];
  filteredTemplates: ResponseTemplate[] = [];

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;
  totalPages = 0;

  // UI State
  loading = false;
  error: string | null = null;
  success: string | null = null;
  selectedTemplate: ResponseTemplate | null = null;
  activeTab: 'templates' | 'variables' = 'templates';
  viewMode: 'list' | 'editor' = 'list';

  // Filter/Search
  searchTerm = '';
  selectedCategory = '';
  selectedLanguage = '';
  showActiveOnly = true;

  // Available options
  availableLanguages = this.templateService.getAvailableLanguages();
  defaultCategories = this.templateService.getDefaultCategories();

  constructor() {
    this.initializeForms();
  }

  ngOnInit(): void {
    this.loadInitialData();
    this.setupSearchSubscription();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    this.searchForm = this.fb.group({
      searchTerm: [''],
      category: [''],
      language: [''],
      showActiveOnly: [true]
    });
  }

  private setupSearchSubscription(): void {
    this.searchForm.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.onSearch();
      });
  }

  private loadInitialData(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      templates: this.templateService.getTemplates({ pageSize: this.pageSize }),
      categories: this.templateService.getCategories(),
      variables: this.templateService.getVariables()
    })
    .pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        this.handleTemplateResponse(data.templates);
        this.categories = [...this.defaultCategories, ...data.categories];
        this.variables = data.variables;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading initial data:', error);
        this.error = 'Failed to load template data. Please try again.';
        this.loading = false;
        this.loadFallbackData();
      }
    });
  }

  private loadFallbackData(): void {
    this.categories = this.defaultCategories;
    this.templates = [];
    this.variables = [];
  }

  private handleTemplateResponse(response: TemplateListResponse): void {
    // Ensure response.templates is an array
    this.templates = Array.isArray(response?.templates) ? response.templates : [];
    this.filteredTemplates = [...this.templates];
    this.totalItems = response?.totalCount || 0;
    this.totalPages = response?.totalPages || 0;
    this.currentPage = response?.page || 1;
  }

  onSearch(): void {
    const formValue = this.searchForm.value;

    const searchParams: TemplateSearchParams = {
      search: formValue.searchTerm || undefined,
      category: formValue.category || undefined,
      language: formValue.language || undefined,
      isActive: formValue.showActiveOnly ? true : undefined,
      page: 1, // Reset to first page on search
      pageSize: this.pageSize
    };

    this.loadTemplates(searchParams);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.loadTemplates({
      page: this.currentPage,
      pageSize: this.pageSize,
      search: this.searchForm.get('searchTerm')?.value || undefined,
      category: this.searchForm.get('category')?.value || undefined,
      language: this.searchForm.get('language')?.value || undefined,
      isActive: this.searchForm.get('showActiveOnly')?.value ? true : undefined
    });
  }

  private loadTemplates(params: TemplateSearchParams): void {
    this.loading = true;

    this.templateService.getTemplates(params)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.handleTemplateResponse(response);
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading templates:', error);
          this.error = 'Failed to load templates. Please try again.';
          this.loading = false;
        }
      });
  }

  onClearSearch(): void {
    this.searchForm.reset({
      searchTerm: '',
      category: '',
      language: '',
      showActiveOnly: true
    });
  }

  onCreateTemplate(): void {
    this.selectedTemplate = null;
    this.viewMode = 'editor';
  }

  onEditTemplate(template: ResponseTemplate): void {
    this.selectedTemplate = template;
    this.viewMode = 'editor';
  }

  onDuplicateTemplate(template: ResponseTemplate): void {
    const duplicatedTemplate: CreateTemplateRequest = {
      templateKey: `${template.templateKey}_copy`,
      category: template.category,
      language: template.language,
      template: template.template,
      isActive: false, // Start as inactive
      priority: template.priority
    };

    this.templateService.createTemplate(duplicatedTemplate)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (newTemplate) => {
          this.success = 'Template duplicated successfully!';
          this.loadTemplates({ page: this.currentPage, pageSize: this.pageSize });
          setTimeout(() => this.success = null, 3000);
        },
        error: (error) => {
          console.error('Error duplicating template:', error);
          this.error = 'Failed to duplicate template. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  onDeleteTemplate(template: ResponseTemplate): void {
    if (confirm(`Are you sure you want to delete the template "${template.templateKey}"? This action cannot be undone.`)) {
      this.templateService.deleteTemplate(template.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.success = 'Template deleted successfully!';
            this.loadTemplates({ page: this.currentPage, pageSize: this.pageSize });
            setTimeout(() => this.success = null, 3000);
          },
          error: (error) => {
            console.error('Error deleting template:', error);
            this.error = 'Failed to delete template. Please try again.';
            setTimeout(() => this.error = null, 5000);
          }
        });
    }
  }

  onToggleTemplateStatus(template: ResponseTemplate): void {
    const updateRequest: UpdateTemplateRequest = {
      templateKey: template.templateKey,
      category: template.category,
      language: template.language,
      template: template.template,
      isActive: !template.isActive,
      priority: template.priority
    };

    this.templateService.updateTemplate(template.id, updateRequest)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (updatedTemplate) => {
          // Update the local template
          const index = this.templates.findIndex(t => t.id === template.id);
          if (index !== -1) {
            this.templates[index] = updatedTemplate;
            this.filteredTemplates = [...this.templates];
          }

          this.success = `Template ${updatedTemplate.isActive ? 'activated' : 'deactivated'} successfully!`;
          setTimeout(() => this.success = null, 3000);
        },
        error: (error) => {
          console.error('Error updating template status:', error);
          this.error = 'Failed to update template status. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  onBackToList(): void {
    this.viewMode = 'list';
    this.selectedTemplate = null;
    // Refresh the list to show any changes
    this.loadTemplates({ page: this.currentPage, pageSize: this.pageSize });
  }

  onTemplateSaved(): void {
    this.success = 'Template saved successfully!';
    this.viewMode = 'list';
    this.selectedTemplate = null;
    this.loadTemplates({ page: this.currentPage, pageSize: this.pageSize });
    setTimeout(() => this.success = null, 3000);
  }

  onVariablesSaved(): void {
    this.success = 'Variables saved successfully!';
    this.templateService.getVariables()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (variables) => {
          this.variables = variables;
        },
        error: (error) => {
          console.error('Error reloading variables:', error);
        }
      });
    setTimeout(() => this.success = null, 3000);
  }

  onTabChange(tab: 'templates' | 'variables'): void {
    this.activeTab = tab;
    this.viewMode = 'list';
    this.selectedTemplate = null;
  }

  getCategoryBadgeClass(category: string): string {
    const categoryClasses: { [key: string]: string } = {
      'Service Requests': 'bg-primary',
      'Contextual Responses': 'bg-success',
      'WiFi Support': 'bg-info',
      'Emergency': 'bg-danger',
      'Maintenance': 'bg-warning',
      'Menu': 'bg-secondary',
      'Fallback': 'bg-dark',
      'Time-based': 'bg-primary',
      'Item Requests': 'bg-success'
    };

    return categoryClasses[category] || 'bg-secondary';
  }

  getLanguageName(code: string): string {
    const language = this.availableLanguages.find(lang => lang.code === code);
    return language ? language.name : code.toUpperCase();
  }

  truncateText(text: string, maxLength: number = 100): string {
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength) + '...';
  }

  get hasFilters(): boolean {
    const formValue = this.searchForm.value;
    return !!(formValue.searchTerm || formValue.category || formValue.language || !formValue.showActiveOnly);
  }

  get filteredTemplateCount(): number {
    return this.filteredTemplates.length;
  }

  get totalTemplateCount(): number {
    return this.totalItems;
  }

  trackByTemplateId(index: number, template: ResponseTemplate): number {
    return template.id;
  }

  // Helper for template
  Math = Math;
}