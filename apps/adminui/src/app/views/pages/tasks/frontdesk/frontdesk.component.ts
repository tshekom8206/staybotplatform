import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbProgressbarModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { TaskService } from '../../../../core/services/task.service';
import { SignalRService } from '../../../../core/services/signalr.service';
import { WhatsAppService } from '../../../../core/services/whatsapp.service';
import {
  StaffTask,
  TaskStatus,
  TaskPriority
} from '../../../../core/models/task.model';

@Component({
  selector: 'app-frontdesk',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NgbDropdownModule,
    NgbPaginationModule,
    NgbTooltipModule,
    NgbProgressbarModule,
    FeatherIconDirective
  ],
  templateUrl: './frontdesk.component.html',
  styleUrl: './frontdesk.component.scss'
})
export class FrontdeskComponent implements OnInit, OnDestroy {
  @ViewChild('taskDetailsModal') taskDetailsModal!: TemplateRef<any>;
  @ViewChild('sendMessageModal') sendMessageModal!: TemplateRef<any>;

  private destroy$ = new Subject<void>();
  private taskService = inject(TaskService);
  private signalRService = inject(SignalRService);
  private modalService = inject(NgbModal);
  private whatsAppService = inject(WhatsAppService);

  // Data properties
  frontdeskTasks: StaffTask[] = [];
  filteredTasks: StaffTask[] = [];
  loading = true;
  error: string | null = null;

  // Department statistics
  frontdeskStats = {
    totalTasks: 0,
    pendingTasks: 0,
    completedTasks: 0,
    guestRequests: 0,
    checkInTasks: 0,
    checkOutTasks: 0
  };

  // Filter properties
  searchTerm = '';
  statusFilter: TaskStatus | 'all' = 'all';
  priorityFilter: TaskPriority | 'all' = 'all';
  serviceTypeFilter: 'all' | 'checkin' | 'checkout' | 'concierge' | 'billing' | 'general' = 'all';

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  readonly statuses: TaskStatus[] = ['Pending', 'InProgress', 'Completed', 'OnHold'];
  readonly priorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent'];

  // Front desk service types
  readonly serviceTypes = [
    { value: 'checkin', label: 'Check-In', icon: 'log-in' },
    { value: 'checkout', label: 'Check-Out', icon: 'log-out' },
    { value: 'concierge', label: 'Concierge', icon: 'bell' },
    { value: 'billing', label: 'Billing', icon: 'credit-card' },
    { value: 'general', label: 'General Service', icon: 'help-circle' }
  ];

  ngOnInit(): void {
    this.loadFrontdeskData();
    this.setupRealTimeUpdates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadFrontdeskData(): void {
    this.loading = true;
    this.error = null;

    // Use real API with frontdesk department filter
    this.taskService.getTasksByDepartment('FrontDesk')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tasks) => {
          this.frontdeskTasks = tasks;
          this.calculateFrontdeskStats();
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading frontdesk tasks:', error);
          this.error = 'Failed to load frontdesk tasks. Please try again.';
          this.loading = false;
        }
      });
  }

  private setupRealTimeUpdates(): void {
    this.signalRService.taskUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshData();
      });
  }

  private calculateFrontdeskStats(): void {
    const total = this.frontdeskTasks.length;
    const pending = this.frontdeskTasks.filter(t => t.status === 'Pending').length;
    const completed = this.frontdeskTasks.filter(t => t.status === 'Completed').length;

    // Calculate real service type stats based on task titles
    const checkInTasks = this.frontdeskTasks.filter(t => {
      const title = t.title?.toLowerCase() || '';
      return title.includes('check') && title.includes('in');
    }).length;

    const checkOutTasks = this.frontdeskTasks.filter(t => {
      const title = t.title?.toLowerCase() || '';
      return title.includes('check') && title.includes('out');
    }).length;

    const guestRequests = this.frontdeskTasks.filter(t => {
      const title = t.title?.toLowerCase() || '';
      return title.includes('request') || title.includes('guest') || title.includes('reservation') || title.includes('billing');
    }).length;

    this.frontdeskStats = {
      totalTasks: total,
      pendingTasks: pending,
      completedTasks: completed,
      guestRequests: guestRequests,
      checkInTasks: checkInTasks,
      checkOutTasks: checkOutTasks
    };
  }

  applyFilters(): void {
    let filtered = [...this.frontdeskTasks];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(task =>
        task.title.toLowerCase().includes(term) ||
        task.description.toLowerCase().includes(term) ||
        task.guestName?.toLowerCase().includes(term) ||
        task.roomNumber?.toLowerCase().includes(term)
      );
    }

    // Status filter
    if (this.statusFilter !== 'all') {
      filtered = filtered.filter(task => task.status === this.statusFilter);
    }

    // Priority filter
    if (this.priorityFilter !== 'all') {
      filtered = filtered.filter(task => task.priority === this.priorityFilter);
    }

    // Service type filter
    if (this.serviceTypeFilter !== 'all') {
      filtered = filtered.filter(task => {
        const title = task.title.toLowerCase();
        switch (this.serviceTypeFilter) {
          case 'checkin':
            return title.includes('check') && title.includes('in');
          case 'checkout':
            return title.includes('check') && title.includes('out');
          case 'concierge':
            return title.includes('concierge') || title.includes('reservation') || title.includes('booking');
          case 'billing':
            return title.includes('bill') || title.includes('payment') || title.includes('charge');
          case 'general':
            return !title.includes('check') && !title.includes('bill') && !title.includes('reservation');
          default:
            return true;
        }
      });
    }

    this.filteredTasks = filtered;
    this.totalItems = filtered.length;
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = 'all';
    this.priorityFilter = 'all';
    this.serviceTypeFilter = 'all';
    this.currentPage = 1;
    this.applyFilters();
  }

  // Utility methods
  getStatusClass(status: TaskStatus): string {
    switch (status) {
      case 'Completed': return 'badge bg-success';
      case 'InProgress': return 'badge bg-info';
      case 'Pending': return 'badge bg-warning';
      case 'OnHold': return 'badge bg-secondary';
      default: return 'badge bg-light';
    }
  }

  getPriorityClass(priority: TaskPriority): string {
    switch (priority) {
      case 'Urgent': return 'text-danger fw-bold';
      case 'High': return 'text-warning fw-bold';
      case 'Medium': return 'text-primary';
      case 'Low': return 'text-secondary';
      default: return 'text-muted';
    }
  }

  getServiceIcon(task: StaffTask): string {
    const title = task.title.toLowerCase();

    if (title.includes('check') && title.includes('in')) return 'log-in';
    if (title.includes('check') && title.includes('out')) return 'log-out';
    if (title.includes('reservation') || title.includes('booking')) return 'bell';
    if (title.includes('bill') || title.includes('payment')) return 'credit-card';

    return 'user';
  }

  getServiceType(task: StaffTask): string {
    const title = task.title.toLowerCase();

    if (title.includes('check') && title.includes('in')) return 'Check-In';
    if (title.includes('check') && title.includes('out')) return 'Check-Out';
    if (title.includes('reservation') || title.includes('booking')) return 'Concierge';
    if (title.includes('bill') || title.includes('payment')) return 'Billing';

    return 'General Service';
  }

  // Pagination
  get paginatedTasks(): StaffTask[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    return this.filteredTasks.slice(startIndex, endIndex);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
  }

  refresh(): void {
    this.refreshData();
  }

  private refreshData(): void {
    this.loadFrontdeskData();
  }

  // Task Actions
  selectedTask: StaffTask | null = null;

  // Message Modal Properties
  messageText = '';
  sendingMessage = false;
  messageTemplates = [
    'Hello! This is regarding your request. We will assist you shortly.',
    'Your request has been processed and will be delivered to your room.',
    'Thank you for your patience. Your request is being handled by our team.',
    'Your airport transfer has been arranged. Please check your email for details.',
    'We have received your request and will update you soon.'
  ];

  viewTaskDetails(task: StaffTask): void {
    this.selectedTask = task;
    this.modalService.open(this.taskDetailsModal, {
      size: 'lg',
      centered: true,
      backdrop: 'static'
    });
  }

  startTask(task: StaffTask): void {
    if (task.status !== 'Pending') return;

    this.taskService.updateTask(task.id, { status: 'InProgress' })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.refreshData();
        },
        error: (error) => {
          console.error('Error starting task:', error);
        }
      });
  }

  completeTask(task: StaffTask): void {
    if (task.status !== 'InProgress') return;

    this.taskService.completeTask(task.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.refreshData();
        },
        error: (error) => {
          console.error('Error completing task:', error);
        }
      });
  }

  callGuest(task: StaffTask): void {
    if (!task.guestPhone) return;

    // For now, just copy phone number to clipboard and show a message
    navigator.clipboard.writeText(task.guestPhone).then(() => {
      console.log('Phone number copied to clipboard:', task.guestPhone);
      // TODO: Integrate with actual phone system
    });
  }

  sendMessage(task: StaffTask): void {
    if (!task.guestPhone) return;

    this.selectedTask = task;
    this.messageText = ''; // Reset message text
    this.modalService.open(this.sendMessageModal, {
      size: 'lg',
      centered: true,
      backdrop: 'static'
    });
  }

  useTemplate(template: string): void {
    this.messageText = template;
  }

  sendWhatsAppMessage(): void {
    if (!this.selectedTask || !this.selectedTask.guestPhone || !this.messageText.trim() || this.sendingMessage) {
      return;
    }

    this.sendingMessage = true;

    const messageData = {
      phoneNumber: this.selectedTask.guestPhone,
      message: this.messageText,
      taskId: this.selectedTask.id
    };

    // Call the actual WhatsApp API
    this.whatsAppService.sendMessage(messageData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.sendingMessage = false;
          if (response.message === 'Message sent successfully') {
            alert(`✅ Message sent successfully to ${this.selectedTask?.guestName || this.selectedTask?.guestPhone}!`);
            this.messageText = '';
          } else {
            alert(`❌ Failed to send message: ${response.message}`);
          }
        },
        error: (error) => {
          this.sendingMessage = false;
          console.error('Error sending WhatsApp message:', error);
          alert(`❌ Failed to send message. Please try again.`);
        }
      });
  }

  editTask(task: StaffTask): void {
    // TODO: Implement task editing functionality
    console.log('Edit task:', task);
  }

  // Utility for template
  taskTrackBy(index: number, task: StaffTask): number {
    return task.id;
  }

  Math = Math;
}