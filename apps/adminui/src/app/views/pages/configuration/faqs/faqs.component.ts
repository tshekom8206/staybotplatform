import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  FAQService,
  FAQ,
  CreateFAQRequest,
  UpdateFAQRequest,
  FAQStats
} from '../../../../core/services/faq.service';

@Component({
  selector: 'app-faqs',
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
  templateUrl: './faqs.component.html',
  styleUrl: './faqs.component.scss'
})
export class FAQsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private faqService = inject(FAQService);
  private modalService = inject(NgbModal);

  @ViewChild('faqModal') faqModal!: TemplateRef<any>;

  // Data properties
  faqs: FAQ[] = [];
  filteredFAQs: FAQ[] = [];
  stats: FAQStats | null = null;
  loading = false;
  saving = false;
  error: string | null = null;
  success: string | null = null;

  // Modal properties
  selectedFAQ: FAQ | null = null;
  isEditMode = false;
  faqForm!: FormGroup;

  // Filter and search properties
  searchTerm = '';
  languageFilter = 'all';
  tagFilter = 'all';

  // Configuration data
  availableLanguages: Array<{code: string, name: string}> = [];
  commonTags: string[] = [];
  availableTags: string[] = [];

  ngOnInit(): void {
    this.initializeForm();
    this.loadConfigurationData();
    this.loadFAQs();
    this.loadStats();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.faqForm = this.fb.group({
      question: ['', [Validators.required, Validators.maxLength(500)]],
      answer: ['', [Validators.required, Validators.maxLength(2000)]],
      language: ['en', Validators.required],
      tags: [[]]
    });
  }

  private loadConfigurationData(): void {
    this.availableLanguages = this.faqService.getAvailableLanguages();
    this.commonTags = this.faqService.getCommonTags();
  }

  private loadFAQs(): void {
    this.loading = true;
    this.error = null;

    this.faqService.getFAQs()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (faqs) => {
          this.faqs = faqs;
          this.updateAvailableTags();
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading FAQs:', error);
          this.error = 'Failed to load FAQs. Please try again.';
          this.loading = false;
        }
      });
  }

  private loadStats(): void {
    this.faqService.getFAQStats()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.stats = stats;
        },
        error: (error) => {
          console.error('Error loading FAQ stats:', error);
        }
      });
  }

  private updateAvailableTags(): void {
    const allTags = new Set<string>();
    this.faqs.forEach(faq => {
      faq.tags?.forEach(tag => allTags.add(tag));
    });
    this.availableTags = Array.from(allTags).sort();
  }

  applyFilters(): void {
    let filtered = [...this.faqs];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(faq =>
        faq.question.toLowerCase().includes(term) ||
        faq.answer.toLowerCase().includes(term) ||
        faq.tags?.some(tag => tag.toLowerCase().includes(term))
      );
    }

    // Language filter
    if (this.languageFilter !== 'all') {
      filtered = filtered.filter(faq => faq.language === this.languageFilter);
    }

    // Tag filter
    if (this.tagFilter !== 'all') {
      filtered = filtered.filter(faq => faq.tags?.includes(this.tagFilter));
    }

    this.filteredFAQs = filtered.sort((a, b) => a.question.localeCompare(b.question));
  }

  openCreateModal(): void {
    this.isEditMode = false;
    this.selectedFAQ = null;
    this.faqForm.reset({
      language: 'en',
      tags: []
    });
    this.modalService.open(this.faqModal, { size: 'lg', backdrop: 'static' });
  }

  openEditModal(faq: FAQ): void {
    this.isEditMode = true;
    this.selectedFAQ = faq;
    this.faqForm.patchValue({
      question: faq.question,
      answer: faq.answer,
      language: faq.language,
      tags: faq.tags || []
    });
    this.modalService.open(this.faqModal, { size: 'lg', backdrop: 'static' });
  }

  saveFAQ(): void {
    if (this.faqForm.invalid) {
      this.markFormGroupTouched();
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.faqForm.value;
    const faqData: CreateFAQRequest | UpdateFAQRequest = {
      question: formValue.question,
      answer: formValue.answer,
      language: formValue.language,
      tags: formValue.tags || []
    };

    const operation = this.isEditMode
      ? this.faqService.updateFAQ(this.selectedFAQ!.id, faqData)
      : this.faqService.createFAQ(faqData);

    operation
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (faq) => {
          if (this.isEditMode) {
            const index = this.faqs.findIndex(f => f.id === faq.id);
            if (index !== -1) {
              this.faqs[index] = faq;
            }
          } else {
            this.faqs.push(faq);
          }

          this.updateAvailableTags();
          this.applyFilters();
          this.loadStats(); // Refresh stats
          this.saving = false;
          this.success = `FAQ ${this.isEditMode ? 'updated' : 'created'} successfully!`;
          this.modalService.dismissAll();

          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving FAQ:', error);

          if (error.status === 401) {
            this.error = 'Your session has expired. Please log in again.';
          } else if (error.status === 403) {
            this.error = 'You do not have permission to manage FAQs.';
          } else if (error.status === 400) {
            this.error = 'Invalid data provided. Please check your input and try again.';
          } else {
            this.error = `Failed to ${this.isEditMode ? 'update' : 'create'} FAQ. Please try again.`;
          }

          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  deleteFAQ(faq: FAQ): void {
    if (!confirm(`Are you sure you want to delete this FAQ?\n\nQuestion: "${faq.question}"\n\nThis action cannot be undone.`)) {
      return;
    }

    this.faqService.deleteFAQ(faq.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.faqs = this.faqs.filter(f => f.id !== faq.id);
          this.updateAvailableTags();
          this.applyFilters();
          this.loadStats(); // Refresh stats
          this.success = 'FAQ deleted successfully!';
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error deleting FAQ:', error);
          this.error = 'Failed to delete FAQ. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  addTag(tag: string): void {
    const currentTags = this.faqForm.get('tags')?.value || [];
    if (!currentTags.includes(tag)) {
      this.faqForm.patchValue({
        tags: [...currentTags, tag]
      });
    }
  }

  removeTag(tagToRemove: string): void {
    const currentTags = this.faqForm.get('tags')?.value || [];
    this.faqForm.patchValue({
      tags: currentTags.filter((tag: string) => tag !== tagToRemove)
    });
  }

  private markFormGroupTouched(): void {
    Object.keys(this.faqForm.controls).forEach(field => {
      const control = this.faqForm.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  getLanguageName(code: string): string {
    const language = this.availableLanguages.find(l => l.code === code);
    return language ? language.name : code;
  }

  get uniqueLanguages(): string[] {
    return [...new Set(this.faqs.map(f => f.language))];
  }

  getTagCount(tag: string): number {
    return this.faqs.filter(faq => faq.tags?.includes(tag)).length;
  }

  exportFAQs(): void {
    const dataStr = JSON.stringify(this.filteredFAQs, null, 2);
    const dataUri = 'data:application/json;charset=utf-8,'+ encodeURIComponent(dataStr);

    const exportFileDefaultName = `faqs_${new Date().toISOString().split('T')[0]}.json`;

    const linkElement = document.createElement('a');
    linkElement.setAttribute('href', dataUri);
    linkElement.setAttribute('download', exportFileDefaultName);
    linkElement.click();
  }

  trackByFAQId(index: number, faq: FAQ): number {
    return faq.id;
  }
}