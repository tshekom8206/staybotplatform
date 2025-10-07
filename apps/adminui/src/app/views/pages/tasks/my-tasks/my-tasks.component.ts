import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbProgressbarModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { TaskService } from '../../../../core/services/task.service';
import { SignalRService } from '../../../../core/services/signalr.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  StaffTask,
  TaskFilter,
  UpdateTaskRequest,
  TaskStatus,
  TaskPriority
} from '../../../../core/models/task.model';

@Component({
  selector: 'app-my-tasks',
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
    FeatherIconDirective
  ],
  templateUrl: './my-tasks.component.html',
  styleUrl: './my-tasks.component.scss'
})
export class MyTasksComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private taskService = inject(TaskService);
  private signalRService = inject(SignalRService);
  private authService = inject(AuthService);
  private modalService = inject(NgbModal);
  private formBuilder = inject(FormBuilder);

  @ViewChild('taskDetailModal') taskDetailModal!: TemplateRef<any>;
  @ViewChild('completionModal') completionModal!: TemplateRef<any>;
  @ViewChild('taskDetailsModal') taskDetailsModal!: TemplateRef<any>;

  // Data properties
  myTasks: StaffTask[] = [];
  filteredTasks: StaffTask[] = [];
  loading = true;
  error: string | null = null;

  // Personal statistics
  personalStats = {
    total: 0,
    pending: 0,
    inProgress: 0,
    completed: 0,
    overdue: 0,
    completionRate: 0,
    avgCompletionTime: 0
  };

  // Filter properties
  searchTerm = '';
  statusFilter: TaskStatus | 'all' = 'all';
  priorityFilter: TaskPriority | 'all' = 'all';
  timeFilter: 'all' | 'overdue' | 'today' | 'this-week' = 'all';
  viewMode: 'all' | 'active' | 'completed' = 'active';

  // Pagination properties
  currentPage = 1;
  pageSize = 8;
  totalItems = 0;

  // Sort properties
  sortField: keyof StaffTask = 'estimatedCompletionTime';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Modal properties
  selectedTask: StaffTask | null = null;
  completionForm!: FormGroup;
  editTaskForm!: FormGroup;

  // Quick actions
  quickActions = [
    { label: 'Mark In Progress', status: 'InProgress' as TaskStatus, icon: 'play-circle', class: 'btn-info' },
    { label: 'Mark Completed', status: 'Completed' as TaskStatus, icon: 'check-circle', class: 'btn-success' },
    { label: 'Put On Hold', status: 'OnHold' as TaskStatus, icon: 'pause-circle', class: 'btn-warning' }
  ];

  // Constants
  readonly statuses: TaskStatus[] = ['Pending', 'InProgress', 'Completed', 'Cancelled', 'OnHold'];
  readonly priorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent'];

  ngOnInit(): void {
    this.initializeForms();
    this.loadMyTasks();
    this.setupRealTimeUpdates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    this.completionForm = this.formBuilder.group({
      completionNotes: ['', [Validators.required, Validators.minLength(5)]],
      timeSpent: [null, [Validators.min(1)]],
      feedback: ['']
    });

    this.editTaskForm = this.formBuilder.group({
      status: ['', Validators.required],
      priority: ['', Validators.required],
      estimatedCompletionTime: [''],
      notes: ['']
    });
  }

  private loadMyTasks(): void {
    this.loading = true;
    this.error = null;

    const filter = this.buildFilter();
    this.taskService.getMyTasks(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tasks) => {
          this.myTasks = tasks;
          this.calculatePersonalStats();
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading my tasks:', error);
          this.error = 'Failed to load your tasks. Please try again.';
          this.loading = false;
        }
      });
  }

  private setupRealTimeUpdates(): void {
    // Listen for task updates via SignalR
    this.signalRService.taskUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshData();
      });

    this.signalRService.taskAssigned$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshData();
      });
  }

  private buildFilter(): TaskFilter {
    const filter: TaskFilter = {};

    if (this.statusFilter !== 'all') filter.status = this.statusFilter;
    if (this.priorityFilter !== 'all') filter.priority = this.priorityFilter;
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

  private calculatePersonalStats(): void {
    const total = this.myTasks.length;
    const pending = this.myTasks.filter(t => t.status === 'Pending').length;
    const inProgress = this.myTasks.filter(t => t.status === 'InProgress').length;
    const completed = this.myTasks.filter(t => t.status === 'Completed').length;
    const now = new Date();
    const overdue = this.myTasks.filter(t =>
      t.status !== 'Completed' &&
      t.estimatedCompletionTime &&
      t.estimatedCompletionTime < now
    ).length;

    // Calculate completion rate for tasks with due dates
    const tasksWithDueDates = this.myTasks.filter(t => t.estimatedCompletionTime);
    const completionRate = tasksWithDueDates.length > 0
      ? Math.round((completed / tasksWithDueDates.length) * 100)
      : 0;

    // Calculate average completion time from real data
    const completedTasks = this.myTasks.filter(t => t.status === 'Completed' && t.createdAt && t.completedAt);
    const avgCompletionTime = completedTasks.length > 0
      ? completedTasks.reduce((sum, task) => {
          const timeDiff = task.completedAt!.getTime() - task.createdAt.getTime();
          return sum + (timeDiff / (1000 * 60)); // Convert to minutes
        }, 0) / completedTasks.length
      : 0;

    this.personalStats = {
      total,
      pending,
      inProgress,
      completed,
      overdue,
      completionRate,
      avgCompletionTime
    };
  }

  applyFilters(): void {
    let filtered = [...this.myTasks];

    // View mode filter
    if (this.viewMode === 'active') {
      filtered = filtered.filter(task => task.status !== 'Completed' && task.status !== 'Cancelled');
    } else if (this.viewMode === 'completed') {
      filtered = filtered.filter(task => task.status === 'Completed');
    }

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(task =>
        task.title.toLowerCase().includes(term) ||
        task.description.toLowerCase().includes(term) ||
        task.roomNumber?.toLowerCase().includes(term) ||
        task.guestName?.toLowerCase().includes(term)
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
    this.statusFilter = 'all';
    this.priorityFilter = 'all';
    this.timeFilter = 'all';
    this.currentPage = 1;
    this.applyFilters();
  }

  // Task management methods
  quickUpdateStatus(task: StaffTask, newStatus: TaskStatus): void {
    if (newStatus === 'Completed') {
      this.openCompletionModal(task);
      return;
    }

    const request: UpdateTaskRequest = {
      status: newStatus
    };

    this.taskService.updateTask(task.id, request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.refreshData();
        },
        error: (error) => {
          console.error('Error updating task status:', error);
        }
      });
  }

  completeTaskWithNotes(): void {
    if (this.completionForm.valid && this.selectedTask) {
      const formValue = this.completionForm.value;
      const request: UpdateTaskRequest = {
        status: 'Completed',
        notes: formValue.completionNotes
      };

      this.taskService.updateTask(this.selectedTask.id, request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            this.completionForm.reset();
            this.refreshData();
          },
          error: (error) => {
            console.error('Error completing task:', error);
          }
        });
    }
  }

  updateTask(): void {
    if (this.editTaskForm.valid && this.selectedTask) {
      const formValue = this.editTaskForm.value;
      const request: UpdateTaskRequest = {
        status: formValue.status,
        priority: formValue.priority,
        estimatedCompletionTime: formValue.estimatedCompletionTime ?
          new Date(formValue.estimatedCompletionTime) : undefined,
        notes: formValue.notes || undefined
      };

      this.taskService.updateTask(this.selectedTask.id, request)
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

  // Modal methods
  openCompletionModal(task: StaffTask): void {
    this.selectedTask = task;
    this.modalService.open(this.completionModal, { size: 'md', backdrop: 'static' });
  }

  openTaskDetailModal(task: StaffTask): void {
    this.selectedTask = task;
    this.editTaskForm.patchValue({
      status: task.status,
      priority: task.priority,
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

  getDepartmentIcon(department: string): string {
    switch (department) {
      case 'Housekeeping': return 'home';
      case 'Maintenance': return 'tool';
      case 'FrontDesk': return 'user';
      case 'Concierge': return 'bell';
      case 'FoodService': return 'coffee';
      default: return 'layers';
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

  getProgressPercentage(): number {
    if (this.personalStats.total === 0) return 0;
    return Math.round((this.personalStats.completed / this.personalStats.total) * 100);
  }

  getPriorityCount(priority: TaskPriority): number {
    return this.myTasks.filter(task =>
      task.priority === priority &&
      task.status !== 'Completed' &&
      task.status !== 'Cancelled'
    ).length;
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
  }

  refresh(): void {
    this.refreshData();
  }

  private refreshData(): void {
    this.loadMyTasks();
  }

  // Utility for template
  taskTrackBy(index: number, task: StaffTask): number {
    return task.id;
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
    this.quickUpdateStatus(task, 'InProgress');
  }

  completeTask(task: StaffTask): void {
    if (task.status !== 'InProgress') return;
    this.openCompletionModal(task);
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

  editTask(task: StaffTask): void {
    this.openTaskDetailModal(task);
  }

  Math = Math;
}