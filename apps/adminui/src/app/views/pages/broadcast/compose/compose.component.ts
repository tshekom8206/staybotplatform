import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbModalModule, NgbAlertModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { QuillModule } from 'ngx-quill';
import { BroadcastService, RecipientGroup } from '../../../../core/services/broadcast.service';


@Component({
  selector: 'app-compose',
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
  templateUrl: './compose.component.html',
  styleUrl: './compose.component.scss'
})
export class ComposeComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private broadcastService = inject(BroadcastService);

  messageForm: FormGroup;
  loading = false;
  error: string | null = null;
  success: string | null = null;

  // Available recipients - loaded from API
  availableRecipients: RecipientGroup[] = [];
  selectedRecipients: RecipientGroup[] = [];

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

  // Preview mode
  previewMode = false;
  previewContent = '';

  constructor() {
    this.initializeForm();
  }

  ngOnInit(): void {
    // Load available recipients from API
    this.loadRecipients();
    // Load any draft message from localStorage
    this.loadDraft();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    // Auto-save draft when component is destroyed
    this.saveDraft();
  }

  private initializeForm(): void {
    this.messageForm = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(100)]],
      content: ['', [Validators.required, Validators.minLength(10)]],
      priority: ['medium', Validators.required],
      scheduleFor: [null], // null means send immediately
      customRooms: [''] // For custom room selection
    });

    // Auto-save draft every 30 seconds
    this.messageForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.saveDraftDebounced();
      });
  }

  private saveDraftDebounced = this.debounce(() => {
    this.saveDraft();
  }, 30000);

  private debounce(func: Function, wait: number) {
    let timeout: any;
    return function executedFunction(...args: any[]) {
      const later = () => {
        clearTimeout(timeout);
        func(...args);
      };
      clearTimeout(timeout);
      timeout = setTimeout(later, wait);
    };
  }

  private saveDraft(): void {
    const draft = {
      ...this.messageForm.value,
      selectedRecipients: this.selectedRecipients,
      lastSaved: new Date()
    };
    localStorage.setItem('broadcast_draft', JSON.stringify(draft));
  }

  private loadRecipients(): void {
    this.broadcastService.getRecipientGroups()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.success && response.data) {
            this.availableRecipients = response.data;
          }
        },
        error: (error) => {
          console.error('Error loading recipients:', error);
          this.error = 'Failed to load recipient groups';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  private loadDraft(): void {
    const draft = localStorage.getItem('broadcast_draft');
    if (draft) {
      try {
        const draftData = JSON.parse(draft);
        this.messageForm.patchValue(draftData);
        this.selectedRecipients = draftData.selectedRecipients || [];
      } catch (error) {
        console.error('Error loading draft:', error);
      }
    }
  }

  addRecipient(recipient: RecipientGroup): void {
    if (!this.selectedRecipients.find(r => r.id === recipient.id)) {
      this.selectedRecipients.push(recipient);
    }
  }

  removeRecipient(recipientId: string): void {
    this.selectedRecipients = this.selectedRecipients.filter(r => r.id !== recipientId);
  }

  addCustomRooms(): void {
    const customRooms = this.messageForm.get('customRooms')?.value;
    if (customRooms && customRooms.trim()) {
      const rooms = customRooms.split(',').map((room: string) => room.trim());
      const customRecipient: RecipientGroup = {
        id: `custom-${Date.now()}`,
        type: 'custom',
        name: `Rooms: ${rooms.join(', ')}`,
        description: `Custom room selection: ${rooms.length} rooms`,
        count: rooms.length
      };
      this.addRecipient(customRecipient);
      this.messageForm.get('customRooms')?.setValue('');
    }
  }

  getTotalRecipientCount(): number {
    return this.selectedRecipients.reduce((total, recipient) => total + (recipient.count || 0), 0);
  }

  getPriorityClass(priority: string): string {
    switch (priority) {
      case 'urgent': return 'badge bg-danger';
      case 'high': return 'badge bg-warning';
      case 'medium': return 'badge bg-info';
      case 'low': return 'badge bg-secondary';
      default: return 'badge bg-info';
    }
  }

  getPriorityIcon(priority: string): string {
    switch (priority) {
      case 'urgent': return 'alert-triangle';
      case 'high': return 'alert-circle';
      case 'medium': return 'info';
      case 'low': return 'minus-circle';
      default: return 'info';
    }
  }

  togglePreview(): void {
    if (!this.previewMode) {
      this.previewContent = this.messageForm.get('content')?.value || '';
    }
    this.previewMode = !this.previewMode;
  }

  saveDraftManually(): void {
    this.saveDraft();
    this.success = 'Draft saved successfully';
    setTimeout(() => this.success = null, 3000);
  }

  clearDraft(): void {
    localStorage.removeItem('broadcast_draft');
    this.messageForm.reset();
    this.selectedRecipients = [];
    this.messageForm.patchValue({ priority: 'medium' });
    this.success = 'Draft cleared';
    setTimeout(() => this.success = null, 3000);
  }

  sendMessage(): void {
    if (this.messageForm.invalid) {
      this.markFormGroupTouched();
      return;
    }

    if (this.selectedRecipients.length === 0) {
      this.error = 'Please select at least one recipient group';
      setTimeout(() => this.error = null, 5000);
      return;
    }

    const formValue = this.messageForm.value;
    const request = {
      title: formValue.title,
      content: formValue.content,
      recipients: this.selectedRecipients.map(r => r.id),
      priority: formValue.priority,
      scheduledAt: formValue.scheduleFor
    };

    this.loading = true;
    this.error = null;

    this.broadcastService.sendGeneralBroadcast(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.loading = false;
          if (response.success && response.data) {
            this.success = response.data.message;

            // Clear form after successful send
            this.messageForm.reset();
            this.selectedRecipients = [];
            this.messageForm.patchValue({ priority: 'medium' });
            localStorage.removeItem('broadcast_draft');

            // Clear success message after 5 seconds
            setTimeout(() => this.success = null, 5000);
          } else {
            this.error = response.error || 'Failed to send message';
            setTimeout(() => this.error = null, 5000);
          }
        },
        error: (error) => {
          this.loading = false;
          this.error = 'Failed to send message. Please try again.';
          setTimeout(() => this.error = null, 5000);
          console.error('Error sending broadcast:', error);
        }
      });
  }

  scheduleMessage(): void {
    // Open date/time picker modal
    console.log('Schedule message functionality to be implemented');
  }

  private markFormGroupTouched(): void {
    Object.keys(this.messageForm.controls).forEach(field => {
      const control = this.messageForm.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  get isFormValid(): boolean {
    return this.messageForm.valid && this.selectedRecipients.length > 0;
  }

  // Helper method for template to check if recipient is already selected
  isRecipientSelected(recipient: RecipientGroup): boolean {
    return this.selectedRecipients.some(r => r.id === recipient.id);
  }
}