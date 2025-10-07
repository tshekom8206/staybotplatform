import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbProgressbarModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { TaskService } from '../../../../core/services/task.service';
import { SignalRService } from '../../../../core/services/signalr.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  StaffTask,
  TaskFilter,
  CreateTaskRequest,
  UpdateTaskRequest,
  TaskStatus,
  TaskPriority
} from '../../../../core/models/task.model';

@Component({
  selector: 'app-maintenance',
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
  templateUrl: './maintenance.component.html',
  styleUrl: './maintenance.component.scss'
})
export class MaintenanceComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private taskService = inject(TaskService);
  private signalRService = inject(SignalRService);
  private authService = inject(AuthService);
  private modalService = inject(NgbModal);
  private formBuilder = inject(FormBuilder);

  @ViewChild('taskDetailsModal') taskDetailsModal!: TemplateRef<any>;
  @ViewChild('editTaskModal') editTaskModal!: TemplateRef<any>;
  @ViewChild('createTaskModal') createTaskModal!: TemplateRef<any>;

  // Data properties
  maintenanceTasks: StaffTask[] = [];
  filteredTasks: StaffTask[] = [];
  loading = true;
  error: string | null = null;

  // Modal properties
  selectedTask: StaffTask | null = null;
  createTaskForm!: FormGroup;
  editTaskForm!: FormGroup;
  staffUsers: Array<{id: number, displayName: string}> = [];

  // Department statistics
  maintenanceStats = {
    totalTasks: 0,
    pendingTasks: 0,
    inProgressTasks: 0,
    completedTasks: 0,
    overdueTasks: 0,
    urgentTasks: 0,
    highPriorityTasks: 0,
    mediumPriorityTasks: 0,
    lowPriorityTasks: 0,
    completionRate: 0,
    averageRepairTime: 0
  };

  // Filter properties
  searchTerm = '';
  statusFilter: TaskStatus | 'all' = 'all';
  priorityFilter: TaskPriority | 'all' = 'all';
  categoryFilter: 'all' | 'electrical' | 'plumbing' | 'hvac' | 'mechanical' | 'structural' | 'safety' = 'all';
  activeTasksFilter: 'active' | 'all' | 'completed' = 'active';
  timeFilter: 'all' | 'today' | 'week' | 'month' = 'all';

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  // Maintenance-specific task categories
  readonly maintenanceCategories = [
    { value: 'electrical', label: 'Electrical', icon: 'zap' },
    { value: 'plumbing', label: 'Plumbing', icon: 'droplet' },
    { value: 'hvac', label: 'HVAC', icon: 'wind' },
    { value: 'mechanical', label: 'Mechanical', icon: 'settings' },
    { value: 'structural', label: 'Structural', icon: 'home' },
    { value: 'safety', label: 'Safety', icon: 'shield' }
  ];

  readonly statuses: TaskStatus[] = ['Pending', 'InProgress', 'Completed', 'OnHold'];
  readonly priorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent'];

  // Maintenance-specific task types
  // Maintenance-specific task types (loaded from database)
  maintenanceTaskTypes: Array<{value: string, label: string, icon: string, estimatedTime: number}> = [];

  ngOnInit(): void {
    this.initializeForms();
    this.loadMaintenanceTaskTypes();
    this.loadStaffUsers();
    this.loadMaintenanceData();
    this.setupRealTimeUpdates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    this.createTaskForm = this.formBuilder.group({
      taskType: ['', Validators.required],
      title: ['', [Validators.required, Validators.minLength(3)]],
      description: [''],
      priority: ['Medium', Validators.required],
      roomNumber: ['', Validators.required],
      assignedToId: [null],
      estimatedCompletionTime: [''],
      notes: ['']
    });

    this.editTaskForm = this.formBuilder.group({
      title: ['', [Validators.required, Validators.minLength(3)]],
      description: [''],
      priority: ['Medium', Validators.required],
      status: ['Pending', Validators.required],
      roomNumber: [''],
      assignedToId: [null],
      estimatedCompletionTime: [''],
      notes: ['']
    });

    // Auto-fill title and description based on task type for create form
    this.createTaskForm.get('taskType')?.valueChanges.subscribe(taskType => {
      const selectedType = this.maintenanceTaskTypes.find(t => t.value === taskType);
      if (selectedType) {
        this.createTaskForm.patchValue({
          title: selectedType.label
        });

        // Set estimated completion time based on task type
        if (selectedType.estimatedTime) {
          const now = new Date();
          const estimatedTime = new Date(now.getTime() + selectedType.estimatedTime * 60000);
          this.createTaskForm.patchValue({
            estimatedCompletionTime: estimatedTime.toISOString().slice(0, 16)
          });
        }
      }
    });
  }

  private loadMaintenanceTaskTypes(): void {
    // TODO: Replace with actual API call to get task types for Maintenance department
    // Example: this.taskService.getTaskTypesByDepartment('Maintenance')
    // Temporary implementation - will be replaced with actual API call
    this.maintenanceTaskTypes = [
      { value: 'Electrical', label: 'Electrical Repair', icon: 'zap', estimatedTime: 90 },
      { value: 'Plumbing', label: 'Plumbing Issue', icon: 'droplet', estimatedTime: 75 },
      { value: 'HVAC', label: 'HVAC Service', icon: 'wind', estimatedTime: 120 },
      { value: 'Mechanical', label: 'Mechanical Repair', icon: 'settings', estimatedTime: 90 },
      { value: 'Structural', label: 'Structural Issue', icon: 'home', estimatedTime: 150 },
      { value: 'Safety', label: 'Safety Check', icon: 'shield', estimatedTime: 60 },
      { value: 'Appliance', label: 'Appliance Repair', icon: 'cpu', estimatedTime: 45 },
      { value: 'General', label: 'General Maintenance', icon: 'tool', estimatedTime: 60 }
    ];
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

  private loadMaintenanceData(): void {
    this.loading = true;
    this.error = null;

    // Use real API with maintenance department filter
    this.taskService.getTasksByDepartment('Maintenance')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tasks) => {
          this.maintenanceTasks = tasks;
          this.calculateMaintenanceStats();
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading maintenance tasks:', error);
          this.error = 'Failed to load maintenance tasks. Please try again.';
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

  private calculateMaintenanceStats(): void {
    const total = this.maintenanceTasks.length;
    const pending = this.maintenanceTasks.filter(t => t.status === 'Pending').length;
    const inProgress = this.maintenanceTasks.filter(t => t.status === 'InProgress').length;
    const completed = this.maintenanceTasks.filter(t => t.status === 'Completed').length;
    const overdue = this.maintenanceTasks.filter(t => this.isOverdue(t)).length;
    const urgent = this.maintenanceTasks.filter(t => t.priority === 'Urgent').length;
    const high = this.maintenanceTasks.filter(t => t.priority === 'High').length;
    const medium = this.maintenanceTasks.filter(t => t.priority === 'Medium').length;
    const low = this.maintenanceTasks.filter(t => t.priority === 'Low').length;
    const completionRate = total > 0 ? Math.round((completed / total) * 100) : 0;

    this.maintenanceStats = {
      totalTasks: total,
      pendingTasks: pending,
      inProgressTasks: inProgress,
      completedTasks: completed,
      overdueTasks: overdue,
      urgentTasks: urgent,
      highPriorityTasks: high,
      mediumPriorityTasks: medium,
      lowPriorityTasks: low,
      completionRate: completionRate,
      averageRepairTime: 85 // TODO: Calculate from actual data
    };
  }

  applyFilters(): void {
    let filtered = [...this.maintenanceTasks];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(task =>
        task.title.toLowerCase().includes(term) ||
        task.description.toLowerCase().includes(term) ||
        task.roomNumber?.toLowerCase().includes(term) ||
        task.notes?.toLowerCase().includes(term)
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

    // Category filter (based on task title/description)
    if (this.categoryFilter !== 'all') {
      filtered = filtered.filter(task => {
        const title = task.title.toLowerCase();
        const desc = task.description.toLowerCase();
        switch (this.categoryFilter) {
          case 'electrical':
            return title.includes('electrical') || title.includes('light') || title.includes('power') || desc.includes('electrical');
          case 'plumbing':
            return title.includes('plumb') || title.includes('water') || title.includes('leak') || desc.includes('plumb');
          case 'hvac':
            return title.includes('hvac') || title.includes('air') || title.includes('heat') || title.includes('ac') || desc.includes('hvac');
          case 'mechanical':
            return title.includes('mechanical') || title.includes('door') || title.includes('lock') || desc.includes('mechanical');
          case 'structural':
            return title.includes('structural') || title.includes('wall') || title.includes('ceiling') || desc.includes('structural');
          case 'safety':
            return title.includes('safety') || title.includes('fire') || title.includes('alarm') || desc.includes('safety');
          default:
            return true;
        }
      });
    }

    // Active tasks filter
    if (this.activeTasksFilter === 'active') {
      filtered = filtered.filter(task => task.status === 'Pending' || task.status === 'InProgress');
    } else if (this.activeTasksFilter === 'completed') {
      filtered = filtered.filter(task => task.status === 'Completed');
    }

    // Time filter
    if (this.timeFilter !== 'all') {
      const now = new Date();
      const startOfDay = new Date(now.getFullYear(), now.getMonth(), now.getDate());
      const startOfWeek = new Date(startOfDay);
      startOfWeek.setDate(startOfDay.getDate() - startOfDay.getDay());
      const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);

      filtered = filtered.filter(task => {
        const taskDate = new Date(task.createdAt);
        switch (this.timeFilter) {
          case 'today':
            return taskDate >= startOfDay;
          case 'week':
            return taskDate >= startOfWeek;
          case 'month':
            return taskDate >= startOfMonth;
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
    this.categoryFilter = 'all';
    this.activeTasksFilter = 'active';
    this.timeFilter = 'all';
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

  getCategoryIcon(task: StaffTask): string {
    const title = task.title.toLowerCase();
    const desc = task.description.toLowerCase();

    if (title.includes('electrical') || title.includes('light') || title.includes('power')) return 'zap';
    if (title.includes('plumb') || title.includes('water') || title.includes('leak')) return 'droplet';
    if (title.includes('hvac') || title.includes('air') || title.includes('heat') || title.includes('ac')) return 'wind';
    if (title.includes('door') || title.includes('lock') || title.includes('mechanical')) return 'settings';
    if (title.includes('structural') || title.includes('wall') || title.includes('ceiling')) return 'home';
    if (title.includes('safety') || title.includes('fire') || title.includes('alarm')) return 'shield';

    return 'tool';
  }

  getTaskCategory(task: StaffTask): string {
    const title = task.title.toLowerCase();

    if (title.includes('electrical') || title.includes('light') || title.includes('power')) return 'Electrical';
    if (title.includes('plumb') || title.includes('water') || title.includes('leak')) return 'Plumbing';
    if (title.includes('hvac') || title.includes('air') || title.includes('heat') || title.includes('ac')) return 'HVAC';
    if (title.includes('door') || title.includes('lock') || title.includes('mechanical')) return 'Mechanical';
    if (title.includes('structural') || title.includes('wall') || title.includes('ceiling')) return 'Structural';
    if (title.includes('safety') || title.includes('fire') || title.includes('alarm')) return 'Safety';

    return 'General';
  }

  // Helper method for template to count tasks by category
  getTaskCountByCategory(categoryLabel: string): number {
    return this.maintenanceTasks.filter(task =>
      this.getTaskCategory(task).toLowerCase() === categoryLabel.toLowerCase()
    ).length;
  }

  isOverdue(task: StaffTask): boolean {
    if (task.status === 'Completed' || !task.estimatedCompletionTime) return false;
    return task.estimatedCompletionTime < new Date();
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
    this.loadMaintenanceData();
  }

  // Utility for template
  taskTrackBy(index: number, task: StaffTask): number {
    return task.id;
  }

  // Modal methods
  viewTaskDetails(task: StaffTask): void {
    this.selectedTask = task;
    this.modalService.open(this.taskDetailsModal, {
      size: 'lg',
      centered: true,
      backdrop: 'static'
    });
  }

  openTaskDetailsModal(task: StaffTask): void {
    this.selectedTask = task;
    this.modalService.open(this.taskDetailsModal, { size: 'lg', backdrop: 'static' });
  }

  openEditTaskModal(task: StaffTask): void {
    this.selectedTask = task;

    // Populate the edit form with current task data
    this.editTaskForm.patchValue({
      title: task.title,
      description: task.description || '',
      priority: task.priority,
      status: task.status,
      roomNumber: task.roomNumber || '',
      assignedToId: task.assignedToId || null,
      estimatedCompletionTime: task.estimatedCompletionTime
        ? new Date(task.estimatedCompletionTime).toISOString().slice(0, 16)
        : '',
      notes: task.notes || ''
    });

    this.modalService.open(this.editTaskModal, { size: 'lg', backdrop: 'static' });
  }

  openCreateTaskModal(): void {
    this.createTaskForm.reset({ priority: 'Medium' });
    this.modalService.open(this.createTaskModal, { size: 'lg', backdrop: 'static' });
  }

  // Task management methods
  createTask(): void {
    if (this.createTaskForm.valid) {
      const formValue = this.createTaskForm.value;
      const request: CreateTaskRequest = {
        title: formValue.title,
        description: formValue.description || `${formValue.taskType} maintenance task`,
        department: 'Maintenance',
        priority: formValue.priority,
        roomNumber: formValue.roomNumber || undefined,
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
            console.error('Error creating maintenance task:', error);
          }
        });
    }
  }

  updateTask(): void {
    if (this.editTaskForm.valid && this.selectedTask) {
      const formValue = this.editTaskForm.value;
      const request: UpdateTaskRequest = {
        priority: formValue.priority,
        status: formValue.status,
        assignedToId: formValue.assignedToId || undefined,
        estimatedCompletionTime: formValue.estimatedCompletionTime ? new Date(formValue.estimatedCompletionTime) : undefined,
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
            console.error('Error updating maintenance task:', error);
          }
        });
    }
  }

  startTask(task: StaffTask): void {
    const request: UpdateTaskRequest = {
      status: 'InProgress'
    };

    this.taskService.updateTask(task.id, request)
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

  deleteTask(task: StaffTask): void {
    if (confirm(`Are you sure you want to delete the task "${task.title}"?`)) {
      this.taskService.deleteTask(task.id)
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

  // New methods for updated template
  setActiveTasksFilter(filter: 'active' | 'all' | 'completed'): void {
    this.activeTasksFilter = filter;
    this.applyFilters();
  }

  getActiveTasksCount(): number {
    return this.maintenanceTasks.filter(t => t.status === 'Pending' || t.status === 'InProgress').length;
  }

  getPriorityColor(priority: TaskPriority): string {
    switch (priority) {
      case 'Urgent': return '#dc3545';
      case 'High': return '#fd7e14';
      case 'Medium': return '#0dcaf0';
      case 'Low': return '#6c757d';
      default: return '#6c757d';
    }
  }

  // Additional task actions (matching FrontDesk component pattern)
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
    this.openEditTaskModal(task);
  }

  // Make Math available to template
  Math = Math;
}