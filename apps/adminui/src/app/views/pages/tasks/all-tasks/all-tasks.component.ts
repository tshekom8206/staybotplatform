import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbProgressbarModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { TaskService, TaskBulkAction } from '../../../../core/services/task.service';
import { SignalRService } from '../../../../core/services/signalr.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  StaffTask,
  TaskFilter,
  TaskStatistics,
  CreateTaskRequest,
  UpdateTaskRequest,
  Department,
  TaskStatus,
  TaskPriority
} from '../../../../core/models/task.model';

@Component({
  selector: 'app-all-tasks',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    NgbDropdownModule,
    NgbPaginationModule,
    NgbTooltipModule,
    NgbProgressbarModule,
    NgbModalModule,
    FeatherIconDirective
  ],
  templateUrl: './all-tasks.component.html',
  styleUrl: './all-tasks.component.scss'
})
export class AllTasksComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private taskService = inject(TaskService);
  private signalRService = inject(SignalRService);
  private authService = inject(AuthService);
  private modalService = inject(NgbModal);
  private formBuilder = inject(FormBuilder);

  @ViewChild('createTaskModal') createTaskModal!: TemplateRef<any>;
  @ViewChild('taskDetailModal') taskDetailModal!: TemplateRef<any>;
  @ViewChild('taskDetailsModal') taskDetailsModal!: TemplateRef<any>;
  @ViewChild('ratingModal') ratingModal!: TemplateRef<any>;

  // Data properties
  tasks: StaffTask[] = [];
  filteredTasks: StaffTask[] = [];
  statistics: TaskStatistics | null = null;
  loading = true;
  error: string | null = null;

  // Selection properties
  selectedTasks: Set<number> = new Set();
  selectAll = false;

  // Filter properties
  searchTerm = '';
  departmentFilter: Department | 'all' = 'all';
  statusFilter: TaskStatus | 'all' = 'all';
  priorityFilter: TaskPriority | 'all' = 'all';
  assigneeFilter: 'all' | 'assigned' | 'unassigned' | number = 'all';
  timeFilter: 'all' | 'overdue' | 'today' | 'this-week' = 'all';

  // Pagination properties
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  // Sort properties
  sortField: keyof StaffTask = 'createdAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  // Modal properties
  selectedTask: StaffTask | null = null;
  createTaskForm!: FormGroup;
  editTaskForm!: FormGroup;
  ratingForm!: FormGroup;
  currentRatingTask: StaffTask | null = null;
  staffUsers: Array<{id: number, displayName: string}> = [];

  // Constants
  readonly departments: Department[] = ['Housekeeping', 'Maintenance', 'FrontDesk', 'Concierge', 'FoodService', 'General'];
  readonly statuses: TaskStatus[] = ['Pending', 'InProgress', 'Completed', 'Cancelled', 'OnHold'];
  readonly priorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent'];

  ngOnInit(): void {
    this.initializeForms();
    this.loadStaffUsers();
    this.loadTasks();
    this.loadStatistics();
    this.setupRealTimeUpdates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    this.createTaskForm = this.formBuilder.group({
      title: ['', [Validators.required, Validators.minLength(3)]],
      description: ['', [Validators.required, Validators.minLength(5)]],
      department: ['', Validators.required],
      priority: ['Medium', Validators.required],
      roomNumber: [''],
      guestName: [''],
      assignedToId: [null],
      estimatedCompletionTime: [''],
      notes: ['']
    });

    this.editTaskForm = this.formBuilder.group({
      status: ['', Validators.required],
      priority: ['', Validators.required],
      assignedToId: [null],
      estimatedCompletionTime: [''],
      notes: ['']
    });

    this.ratingForm = this.formBuilder.group({
      rating: [5, [Validators.required, Validators.min(1), Validators.max(5)]],
      comment: ['']
    });
  }

  private loadStaffUsers(): void {
    // Load users filtered by current tenant
    const currentTenant = this.authService.currentTenantValue;
    if (!currentTenant) {
      this.staffUsers = [];
      return;
    }

    // Filter users by tenant - hardcoded for now, should be API call
    if (currentTenant.id === 1) {
      // Radisson Blu Hotel Sandton
      this.staffUsers = [
        { id: 14, displayName: 'Test Admin (test@admin.com)' },
        { id: 999, displayName: 'Panorama Admin (admin@panoramaview.com)' }
      ];
    } else if (currentTenant.id === 6) {
      // Minimal Test Hotel
      this.staffUsers = [
        { id: 12, displayName: 'Hotel Owner (owner@minimaltesthotel.com)' },
        { id: 13, displayName: 'Front Desk (frontdesk@minimaltesthotel.com)' }
      ];
    } else {
      this.staffUsers = [];
    }
  }

  private loadTasks(): void {
    this.loading = true;
    this.error = null;

    const filter = this.buildFilter();

    this.taskService.getAllTasks(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tasks) => {
          this.tasks = tasks;
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading tasks:', error);
          this.error = 'Failed to load tasks. Please try again.';
          this.loading = false;
        }
      });
  }

  private loadStatistics(): void {
    const filter = this.buildFilter();
    this.taskService.getTaskStatistics(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.statistics = stats;
        },
        error: (error) => {
          console.error('Error loading statistics:', error);
        }
      });
  }

  private setupRealTimeUpdates(): void {
    // Listen for task updates via SignalR
    this.signalRService.taskCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshData();
      });

    this.signalRService.taskUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshData();
      });

    this.signalRService.taskCompleted$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshData();
      });
  }

  private buildFilter(): TaskFilter {
    const filter: TaskFilter = {};

    if (this.departmentFilter !== 'all') filter.department = this.departmentFilter;
    if (this.statusFilter !== 'all') filter.status = this.statusFilter;
    if (this.priorityFilter !== 'all') filter.priority = this.priorityFilter;
    if (typeof this.assigneeFilter === 'number') filter.assignedToId = this.assigneeFilter;
    if (this.searchTerm) filter.searchTerm = this.searchTerm;

    if (this.timeFilter !== 'all') {
      const now = new Date();
      switch (this.timeFilter) {
        case 'today':
          filter.dateFrom = new Date(now.getFullYear(), now.getMonth(), now.getDate());
          filter.dateTo = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);
          break;
        case 'this-week':
          const startOfWeek = new Date(now);
          startOfWeek.setDate(now.getDate() - now.getDay());
          startOfWeek.setHours(0, 0, 0, 0);
          filter.dateFrom = startOfWeek;
          filter.dateTo = now;
          break;
      }
    }

    return filter;
  }

  applyFilters(): void {
    let filtered = [...this.tasks];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(task =>
        task.title.toLowerCase().includes(term) ||
        task.description.toLowerCase().includes(term) ||
        task.roomNumber?.toLowerCase().includes(term) ||
        task.guestName?.toLowerCase().includes(term) ||
        task.notes?.toLowerCase().includes(term)
      );
    }

    // Department filter
    if (this.departmentFilter !== 'all') {
      filtered = filtered.filter(task => task.department === this.departmentFilter);
    }

    // Status filter
    if (this.statusFilter !== 'all') {
      filtered = filtered.filter(task => task.status === this.statusFilter);
    }

    // Priority filter
    if (this.priorityFilter !== 'all') {
      filtered = filtered.filter(task => task.priority === this.priorityFilter);
    }

    // Assignee filter
    if (this.assigneeFilter === 'assigned') {
      filtered = filtered.filter(task => task.assignedToId != null);
    } else if (this.assigneeFilter === 'unassigned') {
      filtered = filtered.filter(task => task.assignedToId == null);
    } else if (typeof this.assigneeFilter === 'number') {
      filtered = filtered.filter(task => task.assignedToId === this.assigneeFilter);
    }

    // Time filter
    if (this.timeFilter === 'overdue') {
      const now = new Date();
      filtered = filtered.filter(task =>
        task.status !== 'Completed' &&
        task.estimatedCompletionTime &&
        task.estimatedCompletionTime < now
      );
    }

    // Apply sorting
    filtered = this.sortTasks(filtered);

    this.filteredTasks = filtered;
    this.totalItems = filtered.length;
    this.selectedTasks.clear();
    this.selectAll = false;
  }

  private sortTasks(tasks: StaffTask[]): StaffTask[] {
    return tasks.sort((a, b) => {
      const aVal = a[this.sortField];
      const bVal = b[this.sortField];

      let comparison = 0;

      if (aVal instanceof Date && bVal instanceof Date) {
        comparison = aVal.getTime() - bVal.getTime();
      } else if (typeof aVal === 'string' && typeof bVal === 'string') {
        comparison = aVal.localeCompare(bVal);
      } else if (typeof aVal === 'number' && typeof bVal === 'number') {
        comparison = aVal - bVal;
      }

      return this.sortDirection === 'desc' ? -comparison : comparison;
    });
  }

  sort(field: keyof StaffTask): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.departmentFilter = 'all';
    this.statusFilter = 'all';
    this.priorityFilter = 'all';
    this.assigneeFilter = 'all';
    this.timeFilter = 'all';
    this.currentPage = 1;
    this.applyFilters();
  }

  // Selection methods
  toggleTaskSelection(taskId: number): void {
    if (this.selectedTasks.has(taskId)) {
      this.selectedTasks.delete(taskId);
    } else {
      this.selectedTasks.add(taskId);
    }
    this.updateSelectAllState();
  }

  toggleSelectAll(): void {
    if (this.selectAll) {
      this.selectedTasks.clear();
    } else {
      this.paginatedTasks.forEach(task => this.selectedTasks.add(task.id));
    }
    this.selectAll = !this.selectAll;
  }

  private updateSelectAllState(): void {
    const paginatedTaskIds = this.paginatedTasks.map(task => task.id);
    this.selectAll = paginatedTaskIds.length > 0 &&
      paginatedTaskIds.every(id => this.selectedTasks.has(id));
  }

  // Task management methods
  createTask(): void {
    if (this.createTaskForm.valid) {
      const formValue = this.createTaskForm.value;
      const request: CreateTaskRequest = {
        title: formValue.title,
        description: formValue.description,
        department: formValue.department,
        priority: formValue.priority,
        roomNumber: formValue.roomNumber || undefined,
        guestName: formValue.guestName || undefined,
        assignedToId: formValue.assignedToId || undefined,
        estimatedCompletionTime: formValue.estimatedCompletionTime ? new Date(formValue.estimatedCompletionTime) : undefined,
        notes: formValue.notes || undefined
      };

      this.taskService.createTask(request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            this.createTaskForm.reset({ priority: 'Medium' });
            this.refreshData();
          },
          error: (error) => {
            console.error('Error creating task:', error);
          }
        });
    }
  }

  updateTask(task: StaffTask): void {
    if (this.editTaskForm.valid) {
      const formValue = this.editTaskForm.value;
      const request: UpdateTaskRequest = {
        status: formValue.status,
        priority: formValue.priority,
        assignedToId: formValue.assignedToId || undefined,
        estimatedCompletionTime: formValue.estimatedCompletionTime ? new Date(formValue.estimatedCompletionTime) : undefined,
        notes: formValue.notes || undefined
      };

      this.taskService.updateTask(task.id, request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            this.refreshData();
          },
          error: (error) => {
            console.error('Error updating task:', error);
          }
        });
    }
  }

  deleteTask(taskId: number): void {
    if (confirm('Are you sure you want to delete this task?')) {
      this.taskService.deleteTask(taskId)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.refreshData();
          },
          error: (error) => {
            console.error('Error deleting task:', error);
          }
        });
    }
  }

  // Bulk operations
  performBulkAssign(assignedToId: number): void {
    if (this.selectedTasks.size === 0) return;

    const action: TaskBulkAction = {
      taskIds: Array.from(this.selectedTasks),
      action: 'assign',
      assignedToId
    };

    this.taskService.performBulkAction(action)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.selectedTasks.clear();
          this.selectAll = false;
          this.refreshData();
        },
        error: (error) => {
          console.error('Error performing bulk assign:', error);
        }
      });
  }

  performBulkStatusUpdate(status: TaskStatus): void {
    if (this.selectedTasks.size === 0) return;

    const action: TaskBulkAction = {
      taskIds: Array.from(this.selectedTasks),
      action: 'updateStatus',
      status
    };

    this.taskService.performBulkAction(action)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.selectedTasks.clear();
          this.selectAll = false;
          this.refreshData();
        },
        error: (error) => {
          console.error('Error performing bulk status update:', error);
        }
      });
  }

  performBulkDelete(): void {
    if (this.selectedTasks.size === 0) return;

    if (confirm(`Are you sure you want to delete ${this.selectedTasks.size} selected tasks?`)) {
      const action: TaskBulkAction = {
        taskIds: Array.from(this.selectedTasks),
        action: 'delete'
      };

      this.taskService.performBulkAction(action)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.selectedTasks.clear();
            this.selectAll = false;
            this.refreshData();
          },
          error: (error) => {
            console.error('Error performing bulk delete:', error);
          }
        });
    }
  }

  // Modal methods
  openCreateTaskModal(): void {
    this.modalService.open(this.createTaskModal, { size: 'lg', backdrop: 'static' });
  }

  openTaskDetailModal(task: StaffTask): void {
    this.selectedTask = task;
    this.editTaskForm.patchValue({
      status: task.status,
      priority: task.priority,
      assignedToId: task.assignedToId,
      estimatedCompletionTime: task.estimatedCompletionTime ?
        task.estimatedCompletionTime.toISOString().slice(0, 16) : '',
      notes: task.notes || ''
    });
    this.modalService.open(this.taskDetailModal, { size: 'lg', backdrop: 'static' });
  }

  // Utility methods
  getStatusClass(status: TaskStatus): string {
    switch (status) {
      case 'Completed': return 'badge bg-success';
      case 'InProgress': return 'badge bg-info';
      case 'Pending': return 'badge bg-warning';
      case 'Cancelled': return 'badge bg-danger';
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

  getDepartmentIcon(department: Department): string {
    switch (department) {
      case 'Housekeeping': return 'home';
      case 'Maintenance': return 'tool';
      case 'FrontDesk': return 'user';
      case 'Concierge': return 'bell';
      case 'FoodService': return 'coffee';
      case 'General': return 'layers';
      default: return 'help-circle';
    }
  }

  isOverdue(task: StaffTask): boolean {
    if (task.status === 'Completed' || !task.estimatedCompletionTime) return false;
    return task.estimatedCompletionTime < new Date();
  }

  getTimeRemaining(task: StaffTask): string {
    if (!task.estimatedCompletionTime || task.status === 'Completed') return '';

    const now = new Date();
    const diff = task.estimatedCompletionTime.getTime() - now.getTime();

    if (diff < 0) {
      const overdue = Math.abs(diff);
      const hours = Math.floor(overdue / (1000 * 60 * 60));
      const minutes = Math.floor((overdue % (1000 * 60 * 60)) / (1000 * 60));

      if (hours > 0) return `${hours}h ${minutes}m overdue`;
      return `${minutes}m overdue`;
    }

    const hours = Math.floor(diff / (1000 * 60 * 60));
    const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));

    if (hours > 0) return `${hours}h ${minutes}m remaining`;
    return `${minutes}m remaining`;
  }

  // Pagination
  get paginatedTasks(): StaffTask[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    return this.filteredTasks.slice(startIndex, endIndex);
  }

  get totalPages(): number {
    return Math.ceil(this.totalItems / this.pageSize);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.updateSelectAllState();
  }

  refresh(): void {
    this.refreshData();
  }

  private refreshData(): void {
    this.loadTasks();
    this.loadStatistics();
  }

  // Additional utility methods
  taskTrackBy(index: number, task: StaffTask): number {
    return task.id;
  }

  completeTask(task: StaffTask): void {
    const request: UpdateTaskRequest = {
      status: 'Completed'
    };

    this.taskService.updateTask(task.id, request)
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

  // Task Actions (matching FrontDesk component pattern)
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

    // TODO: Implement message sending functionality
    console.log('Send message to:', task.guestPhone);
  }

  requestRating(task: StaffTask): void {
    if (!task.guestPhone || task.status !== 'Completed') return;

    this.currentRatingTask = task;
    this.ratingForm.reset({ rating: 5, comment: '' });
    this.modalService.open(this.ratingModal, { size: 'md', backdrop: 'static' });
  }

  submitRating(): void {
    if (!this.ratingForm.valid || !this.currentRatingTask) return;

    const formValue = this.ratingForm.value;

    // First save the rating using the API
    this.taskService.saveTaskRating(this.currentRatingTask.id, formValue.rating, formValue.comment)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          console.log('Rating saved successfully for task:', this.currentRatingTask!.id);

          // Then send the WhatsApp request to the guest
          this.taskService.requestRating(this.currentRatingTask!.id)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: () => {
                console.log('Rating request sent to guest for task:', this.currentRatingTask!.id);
                this.modalService.dismissAll();
                this.refreshData();
              },
              error: (error) => {
                console.error('Error sending rating request to guest:', error);
                this.modalService.dismissAll();
              }
            });
        },
        error: (error: any) => {
          console.error('Error saving rating:', error);
        }
      });
  }

  editTask(task: StaffTask): void {
    this.openTaskDetailModal(task);
  }

  // Math utility for template
  Math = Math;
}