import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbModalModule, NgbAlertModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { QuillModule } from 'ngx-quill';
import { BroadcastService, BroadcastTemplate } from '../../../../core/services/broadcast.service';

export interface MessageTemplate {
  id?: number;
  name: string;
  category: 'welcome' | 'policies' | 'promotions' | 'maintenance' | 'events' | 'general';
  subject: string;
  content: string;
  isActive: boolean;
  isDefault: boolean;
  usageCount: number;
  createdAt: Date | string;
  updatedAt: Date | string;
  createdBy: string;
}

export interface TemplateCategory {
  value: 'welcome' | 'policies' | 'promotions' | 'maintenance' | 'events' | 'general';
  label: string;
  icon: string;
  color: string;
  description: string;
}

@Component({
  selector: 'app-templates',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbModalModule,
    NgbAlertModule,
    FeatherIconDirective,
    QuillModule
  ],
  templateUrl: './templates.component.html',
  styleUrl: './templates.component.scss'
})
export class TemplatesComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private modalService = inject(NgbModal);
  private broadcastService = inject(BroadcastService);

  templateForm: FormGroup;
  loading = false;
  error: string | null = null;
  success: string | null = null;

  // Template management
  templates: MessageTemplate[] = [];
  filteredTemplates: MessageTemplate[] = [];
  selectedTemplate: MessageTemplate | null = null;
  editingTemplate: MessageTemplate | null = null;

  // Filters
  selectedCategory: string = 'all';
  searchTerm: string = '';

  // Template categories
  templateCategories: TemplateCategory[] = [
    {
      value: 'welcome',
      label: 'Welcome Messages',
      icon: 'heart',
      color: '#25d466',
      description: 'Greetings and welcome messages for new guests'
    },
    {
      value: 'policies',
      label: 'Hotel Policies',
      icon: 'file-text',
      color: '#007bff',
      description: 'Hotel rules, policies, and procedures'
    },
    {
      value: 'promotions',
      label: 'Promotions & Offers',
      icon: 'gift',
      color: '#fd7e14',
      description: 'Special offers, discounts, and promotional messages'
    },
    {
      value: 'maintenance',
      label: 'Maintenance Notices',
      icon: 'tool',
      color: '#6f42c1',
      description: 'Maintenance schedules and service interruptions'
    },
    {
      value: 'events',
      label: 'Events & Activities',
      icon: 'calendar',
      color: '#e83e8c',
      description: 'Hotel events, activities, and entertainment'
    },
    {
      value: 'general',
      label: 'General Information',
      icon: 'info',
      color: '#20c997',
      description: 'General announcements and information'
    }
  ];

  // Quill editor configuration
  quillConfig = {
    toolbar: [
      ['bold', 'italic', 'underline'],
      [{ 'list': 'ordered'}, { 'list': 'bullet' }],
      [{ 'header': [1, 2, 3, false] }],
      ['link'],
      ['clean']
    ]
  };

  constructor() {
    this.initializeForm();
  }

  ngOnInit(): void {
    this.loadTemplates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.templateForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      category: ['general', Validators.required],
      subject: ['', [Validators.required, Validators.maxLength(200)]],
      content: ['', [Validators.required, Validators.minLength(10)]],
      isActive: [true],
      isDefault: [false]
    });
  }

  private loadTemplates(): void {
    this.loading = true;
    this.broadcastService.getTemplates()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.success && response.data) {
            this.templates = response.data.map(t => ({
              ...t,
              createdAt: new Date(t.createdAt || ''),
              updatedAt: new Date(t.updatedAt || ''),
              usageCount: t.usageCount || 0,
              createdBy: t.createdBy || 'System'
            })) as MessageTemplate[];
            this.applyFilters();
          }
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading templates:', error);
          this.error = 'Failed to load templates';
          this.loading = false;
          // Fallback to sample data for demo
          this.templates = [];
          this.applyFilters();
        }
      });
  }

  applyFilters(): void {
    let filtered = [...this.templates];

    // Category filter
    if (this.selectedCategory !== 'all') {
      filtered = filtered.filter(template => template.category === this.selectedCategory);
    }

    // Search filter
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(template =>
        template.name.toLowerCase().includes(term) ||
        template.subject.toLowerCase().includes(term) ||
        template.content.toLowerCase().includes(term)
      );
    }

    this.filteredTemplates = filtered.sort((a, b) => {
      const dateA = a.updatedAt instanceof Date ? a.updatedAt : new Date(a.updatedAt);
      const dateB = b.updatedAt instanceof Date ? b.updatedAt : new Date(b.updatedAt);
      return dateB.getTime() - dateA.getTime();
    });
  }

  onCategoryChange(category: string): void {
    this.selectedCategory = category;
    this.applyFilters();
  }

  onSearchChange(searchTerm: string): void {
    this.searchTerm = searchTerm;
    this.applyFilters();
  }

  getCategoryConfig(category: string): TemplateCategory {
    return this.templateCategories.find(c => c.value === category) || this.templateCategories[5];
  }

  openCreateModal(content: any): void {
    this.editingTemplate = null;
    this.templateForm.reset({
      category: 'general',
      isActive: true,
      isDefault: false
    });
    this.modalService.open(content, { size: 'lg', backdrop: 'static' });
  }

  openEditModal(content: any, template: MessageTemplate): void {
    this.editingTemplate = template;
    this.templateForm.patchValue({
      name: template.name,
      category: template.category,
      subject: template.subject,
      content: template.content,
      isActive: template.isActive,
      isDefault: template.isDefault
    });
    this.modalService.open(content, { size: 'lg', backdrop: 'static' });
  }

  saveTemplate(): void {
    if (this.templateForm.invalid) {
      this.markFormGroupTouched();
      return;
    }

    this.loading = true;
    this.error = null;

    const templateData = this.templateForm.value;

    const saveOperation = this.editingTemplate
      ? this.broadcastService.updateTemplate(this.editingTemplate.id!, templateData)
      : this.broadcastService.createTemplate(templateData);

    saveOperation
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.success) {
            this.success = this.editingTemplate
              ? 'Template updated successfully'
              : 'Template created successfully';
            this.loadTemplates();
            this.modalService.dismissAll();
            setTimeout(() => this.success = null, 3000);
          } else {
            this.error = response.error || 'Failed to save template';
          }
          this.loading = false;
        },
        error: (error) => {
          console.error('Error saving template:', error);
          this.error = 'Failed to save template';
          this.loading = false;
        }
      });
  }

  duplicateTemplate(template: MessageTemplate): void {
    const duplicatedData = {
      name: `${template.name} (Copy)`,
      category: template.category,
      subject: template.subject,
      content: template.content,
      isActive: template.isActive,
      isDefault: false
    };

    this.loading = true;
    this.broadcastService.createTemplate(duplicatedData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.success) {
            this.success = 'Template duplicated successfully';
            this.loadTemplates();
            setTimeout(() => this.success = null, 3000);
          } else {
            this.error = response.error || 'Failed to duplicate template';
          }
          this.loading = false;
        },
        error: (error) => {
          console.error('Error duplicating template:', error);
          this.error = 'Failed to duplicate template';
          this.loading = false;
        }
      });
  }

  toggleTemplateStatus(template: MessageTemplate): void {
    const updatedData = {
      name: template.name,
      category: template.category,
      subject: template.subject,
      content: template.content,
      isActive: !template.isActive,
      isDefault: template.isDefault
    };

    this.loading = true;
    this.broadcastService.updateTemplate(template.id!, updatedData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.success) {
            this.success = `Template ${!template.isActive ? 'activated' : 'deactivated'}`;
            this.loadTemplates();
            setTimeout(() => this.success = null, 3000);
          } else {
            this.error = response.error || 'Failed to update template status';
          }
          this.loading = false;
        },
        error: (error) => {
          console.error('Error updating template status:', error);
          this.error = 'Failed to update template status';
          this.loading = false;
        }
      });
  }

  deleteTemplate(template: MessageTemplate): void {
    if (template.isDefault) {
      this.error = 'Cannot delete default templates';
      setTimeout(() => this.error = null, 3000);
      return;
    }

    if (confirm(`Are you sure you want to delete "${template.name}"?`)) {
      this.loading = true;
      this.broadcastService.deleteTemplate(template.id!)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (response) => {
            if (response.success) {
              this.success = 'Template deleted successfully';
              this.loadTemplates();
              setTimeout(() => this.success = null, 3000);
            } else {
              this.error = response.error || 'Failed to delete template';
            }
            this.loading = false;
          },
          error: (error) => {
            console.error('Error deleting template:', error);
            this.error = 'Failed to delete template';
            this.loading = false;
          }
        });
    }
  }

  previewTemplate(template: MessageTemplate): void {
    this.selectedTemplate = template;
  }

  closePreview(): void {
    this.selectedTemplate = null;
  }

  private markFormGroupTouched(): void {
    Object.keys(this.templateForm.controls).forEach(field => {
      const control = this.templateForm.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  getStatusClass(isActive: boolean): string {
    return isActive ? 'badge bg-success' : 'badge bg-secondary';
  }

  getUsageColor(count: number): string {
    if (count > 50) return 'text-success';
    if (count > 10) return 'text-warning';
    return 'text-muted';
  }

  trackByTemplateId(index: number, template: MessageTemplate): string | number {
    return template.id || index;
  }

  get isFormValid(): boolean {
    return this.templateForm.valid;
  }
}