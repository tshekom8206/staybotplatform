import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbProgressbarModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { TaskService } from '../../../../core/services/task.service';
import { SignalRService } from '../../../../core/services/signalr.service';
import { AuthService } from '../../../../core/services/auth.service';
import { AgentService } from '../../../../core/services/agent.service';
import { UserService } from '../../../../core/services/user.service';
import {
  StaffTask,
  TaskFilter,
  CreateTaskRequest,
  UpdateTaskRequest,
  TaskStatus,
  TaskPriority
} from '../../../../core/models/task.model';

interface RoomStatus {
  roomNumber: string;
  status: 'dirty' | 'cleaning' | 'inspecting' | 'clean' | 'ooo'; // out of order
  guestName?: string;
  checkOut?: Date;
  checkIn?: Date;
  priority: TaskPriority;
  tasks: StaffTask[];
  estimatedTime?: number;
  notes?: string;
}

@Component({
  selector: 'app-housekeeping',
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
  templateUrl: './housekeeping.component.html',
  styleUrl: './housekeeping.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HousekeepingComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private taskService = inject(TaskService);
  private signalRService = inject(SignalRService);
  private authService = inject(AuthService);
  private agentService = inject(AgentService);
  private userService = inject(UserService);
  private modalService = inject(NgbModal);
  private formBuilder = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  @ViewChild('createTaskModal') createTaskModal!: TemplateRef<any>;
  @ViewChild('roomDetailModal') roomDetailModal!: TemplateRef<any>;
  @ViewChild('taskDetailsModal') taskDetailsModal!: TemplateRef<any>;
  @ViewChild('taskEditModal') taskEditModal!: TemplateRef<any>;

  // Data properties - Source of truth (never filtered)
  private originalHousekeepingTasks: StaffTask[] = [];
  private originalRoomStatuses: RoomStatus[] = [];

  // Filtered data for display
  housekeepingTasks: StaffTask[] = [];
  filteredTasks: StaffTask[] = [];
  roomStatuses: RoomStatus[] = [];
  loading = true;
  error: string | null = null;

  // Department statistics
  housekeepingStats = {
    totalRooms: 0,
    cleanRooms: 0,
    dirtyRooms: 0,
    inProgressRooms: 0,
    outOfOrderRooms: 0,
    pendingTasks: 0,
    completedTasks: 0,
    averageCleaningTime: 0
  };

  // Filter properties
  searchTerm = '';
  statusFilter: TaskStatus | 'all' = 'all';
  priorityFilter: TaskPriority | 'all' = 'all';
  roomStatusFilter: 'all' | 'dirty' | 'cleaning' | 'inspecting' | 'clean' | 'ooo' = 'all';
  assigneeFilter: 'all' | 'assigned' | 'unassigned' = 'all';
  viewMode: 'tasks' | 'rooms' = 'rooms';

  // Pagination properties
  currentPage = 1;
  pageSize = 12;
  totalItems = 0;

  // Sort properties
  sortField: keyof StaffTask = 'priority';
  sortDirection: 'asc' | 'desc' = 'desc';

  // Modal properties
  selectedRoom: RoomStatus | null = null;
  selectedTask: StaffTask | null = null;

  createTaskForm!: FormGroup;
  editTaskForm!: FormGroup;
  staffUsers: Array<{id: number, displayName: string}> = [];

  // Housekeeping-specific task types (loaded from database)
  housekeepingTaskTypes: Array<{value: string, label: string, icon: string, estimatedTime: number}> = [];

  // Constants
  readonly statuses: TaskStatus[] = ['Pending', 'InProgress', 'Completed', 'OnHold'];
  readonly priorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent'];

  // Math utility for template
  readonly Math = Math;


  ngOnInit(): void {
    this.initializeForms();
    this.loadStaffUsers();
    this.initializeHousekeepingTaskTypes();
    this.loadHousekeepingData();
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
      guestName: [''],
      assignedToId: [null],
      estimatedCompletionTime: [''],
      notes: ['']
    });

    // Auto-fill title and description based on task type
    this.createTaskForm.get('taskType')?.valueChanges.subscribe(taskType => {
      const selectedType = this.housekeepingTaskTypes.find(t => t.value === taskType);
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

    // Initialize edit task form
    this.editTaskForm = this.formBuilder.group({
      status: ['', Validators.required],
      priority: ['', Validators.required],
      assignedToId: [null],
      notes: [''],
      estimatedCompletionTime: ['']
    });
  }

  private loadStaffUsers(): void {
    // Fetch both staff and agents from database
    forkJoin({
      staff: this.userService.getStaffMembers().pipe(
        catchError(error => {
          console.error('Failed to load staff:', error);
          return of([]);
        })
      ),
      agents: this.agentService.getAvailableAgents().pipe(
        catchError(error => {
          console.error('Failed to load agents:', error);
          return of([]);
        })
      )
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ staff, agents }) => {
          // Map staff members to dropdown format
          const staffUsers = staff.map(s => ({
            id: s.id,
            displayName: `${s.userName || s.email} (${s.role})`
          }));

          // Map agents to dropdown format
          const agentUsers = agents.map(a => ({
            id: a.agentId,
            displayName: `${a.name} - Agent (${a.department})`
          }));

          // Combine both lists
          this.staffUsers = [...staffUsers, ...agentUsers];
          this.cdr.markForCheck();
        },
        error: (error) => {
          console.error('Failed to load staff and agents:', error);
          this.staffUsers = [];
          this.cdr.markForCheck();
        }
      });
  }

  private initializeHousekeepingTaskTypes(): void {
    // TODO: Replace with actual API call to get task types for Housekeeping department
    // Example: this.taskService.getTaskTypesByDepartment('Housekeeping')
    //   .pipe(takeUntil(this.destroy$))
    //   .subscribe({
    //     next: (taskTypes) => {
    //       this.housekeepingTaskTypes = taskTypes;
    //     },
    //     error: (error) => {
    //       console.error('Error loading task types:', error);
    //       this.housekeepingTaskTypes = [];
    //     }
    //   });

    // Temporary implementation - will be replaced with actual API call
    this.housekeepingTaskTypes = [
      { value: 'Room Cleaning', label: 'Room Cleaning', icon: 'home', estimatedTime: 45 },
      { value: 'Deep Cleaning', label: 'Deep Cleaning', icon: 'maximize', estimatedTime: 90 },
      { value: 'Linen Change', label: 'Linen Change', icon: 'layers', estimatedTime: 15 },
      { value: 'Amenity Restock', label: 'Amenity Restock', icon: 'package', estimatedTime: 20 },
      { value: 'Maintenance Issue', label: 'Maintenance Issue', icon: 'tool', estimatedTime: 60 },
      { value: 'Lost & Found', label: 'Lost & Found', icon: 'search', estimatedTime: 30 },
      { value: 'Inspection', label: 'Inspection', icon: 'eye', estimatedTime: 20 },
      { value: 'Supplies Request', label: 'Supplies Request', icon: 'shopping-cart', estimatedTime: 10 }
    ];
  }

  private loadHousekeepingData(): void {
    this.loading = true;
    this.error = null;

    const filter: TaskFilter = { department: 'Housekeeping' };
    this.taskService.getTasksByDepartment('Housekeeping', filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tasks) => {
          // Store original source data
          this.originalHousekeepingTasks = [...tasks];
          this.housekeepingTasks = [...tasks];
          this.generateRoomStatuses();
          this.calculateHousekeepingStats();
          this.applyFilters();
          this.loading = false;
          this.cdr.markForCheck(); // Trigger change detection
        },
        error: (error) => {
          console.error('Error loading housekeeping tasks:', error);
          this.error = 'Failed to load housekeeping tasks. Please try again.';
          this.loading = false;
        }
      });
  }


  private generateRoomStatuses(): void {
    // Group tasks by room number
    const roomTaskMap = new Map<string, StaffTask[]>();
    this.housekeepingTasks.forEach(task => {
      if (task.roomNumber) {
        if (!roomTaskMap.has(task.roomNumber)) {
          roomTaskMap.set(task.roomNumber, []);
        }
        roomTaskMap.get(task.roomNumber)!.push(task);
      }
    });

    // Generate room statuses based on tasks and mock data
    const roomStatusesArray = Array.from(roomTaskMap.entries()).map(([roomNumber, tasks]) => {
      const hasInProgressTasks = tasks.some(t => t.status === 'InProgress');
      const hasPendingTasks = tasks.some(t => t.status === 'Pending');
      const highestPriority = this.getHighestPriority(tasks.map(t => t.priority));

      let status: RoomStatus['status'] = 'clean';
      if (hasInProgressTasks) {
        status = 'cleaning';
      } else if (hasPendingTasks) {
        status = 'dirty';
      }

      return {
        roomNumber,
        status,
        priority: highestPriority,
        tasks,
        estimatedTime: this.calculateEstimatedTime(tasks)
      };
    });

    // Store both original and working copies
    this.originalRoomStatuses = [...roomStatusesArray];
    this.roomStatuses = [...roomStatusesArray];
  }

  private setupRealTimeUpdates(): void {
    // Listen for housekeeping task updates with optimized handling
    this.signalRService.taskCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((taskData: any) => {
        this.handleTaskCreatedUpdate(taskData);
      });

    this.signalRService.taskUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((taskData: any) => {
        this.handleTaskUpdatedUpdate(taskData);
      });
  }

  private handleTaskCreatedUpdate(taskData: any): void {
    // Only process if it's a housekeeping task
    if (taskData?.department !== 'Housekeeping') return;

    // Add to source data arrays
    if (taskData) {
      // Convert date strings to Date objects
      const newTask = {
        ...taskData,
        createdAt: new Date(taskData.createdAt),
        updatedAt: taskData.updatedAt ? new Date(taskData.updatedAt) : undefined,
        estimatedCompletionTime: taskData.estimatedCompletionTime ? new Date(taskData.estimatedCompletionTime) : undefined,
        completedAt: taskData.completedAt ? new Date(taskData.completedAt) : undefined
      };

      this.originalHousekeepingTasks.push(newTask);
      this.housekeepingTasks.push(newTask);

      // Update room statuses and recalculate stats
      this.generateRoomStatuses();
      this.calculateHousekeepingStats();
      this.applyFilters();
      this.cdr.markForCheck(); // Trigger change detection for real-time updates
    } else {
      // Fallback to full refresh if we don't have task data
      this.refreshData();
    }
  }

  private handleTaskUpdatedUpdate(taskData: any): void {
    // Only process if it's a housekeeping task
    if (taskData?.department !== 'Housekeeping') return;

    if (taskData?.id) {
      // Update in source data arrays
      const originalTaskIndex = this.originalHousekeepingTasks.findIndex(t => t.id === taskData.id);
      const workingTaskIndex = this.housekeepingTasks.findIndex(t => t.id === taskData.id);

      if (originalTaskIndex !== -1) {
        // Convert date strings to Date objects and update
        const updatedTask = {
          ...taskData,
          createdAt: new Date(taskData.createdAt),
          updatedAt: taskData.updatedAt ? new Date(taskData.updatedAt) : undefined,
          estimatedCompletionTime: taskData.estimatedCompletionTime ? new Date(taskData.estimatedCompletionTime) : undefined,
          completedAt: taskData.completedAt ? new Date(taskData.completedAt) : undefined
        };

        this.originalHousekeepingTasks[originalTaskIndex] = updatedTask;

        if (workingTaskIndex !== -1) {
          this.housekeepingTasks[workingTaskIndex] = updatedTask;
        }

        // Update room statuses and recalculate stats
        this.generateRoomStatuses();
        this.calculateHousekeepingStats();
        this.applyFilters();
        this.cdr.markForCheck(); // Trigger change detection for real-time updates
      } else {
        // Task not found, might be new - fallback to full refresh
        this.refreshData();
      }
    } else {
      // Fallback to full refresh if we don't have task ID
      this.refreshData();
    }
  }

  private calculateHousekeepingStats(): void {
    // Use original unfiltered data for accurate statistics
    const totalRooms = this.originalRoomStatuses.length;
    const cleanRooms = this.originalRoomStatuses.filter(r => r.status === 'clean').length;
    const dirtyRooms = this.originalRoomStatuses.filter(r => r.status === 'dirty').length;
    const inProgressRooms = this.originalRoomStatuses.filter(r => r.status === 'cleaning' || r.status === 'inspecting').length;
    const outOfOrderRooms = this.originalRoomStatuses.filter(r => r.status === 'ooo').length;

    const pendingTasks = this.originalHousekeepingTasks.filter(t => t.status === 'Pending').length;
    const completedTasks = this.originalHousekeepingTasks.filter(t => t.status === 'Completed').length;

    this.housekeepingStats = {
      totalRooms,
      cleanRooms,
      dirtyRooms,
      inProgressRooms,
      outOfOrderRooms,
      pendingTasks,
      completedTasks,
      averageCleaningTime: this.calculateAverageCleaningTime()
    };
  }

  private calculateAverageCleaningTime(): number {
    const completedTasks = this.originalHousekeepingTasks.filter(t =>
      t.status === 'Completed' && t.createdAt && t.completedAt
    );

    if (completedTasks.length === 0) return 0;

    const totalTime = completedTasks.reduce((sum, task) => {
      const timeDiff = task.completedAt!.getTime() - task.createdAt.getTime();
      return sum + (timeDiff / (1000 * 60)); // Convert to minutes
    }, 0);

    return Math.round(totalTime / completedTasks.length);
  }

  getCompletionRate(): number {
    if (this.originalRoomStatuses.length === 0) return 0;

    const completedRooms = this.originalRoomStatuses.filter(r => r.status === 'clean').length;
    return Math.round((completedRooms / this.originalRoomStatuses.length) * 100);
  }

  applyFilters(): void {
    if (this.viewMode === 'rooms') {
      this.applyRoomFilters();
    } else {
      this.applyTaskFilters();
    }
  }

  private applyRoomFilters(): void {
    // Start with original source data, not current filtered data
    let filtered = [...this.originalRoomStatuses];

    // Room status filter
    if (this.roomStatusFilter !== 'all') {
      filtered = filtered.filter(room => room.status === this.roomStatusFilter);
    }

    // Text search for rooms
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(room =>
        room.roomNumber.toLowerCase().includes(term) ||
        room.guestName?.toLowerCase().includes(term) ||
        room.notes?.toLowerCase().includes(term)
      );
    }

    // Update display data without mutating source
    this.roomStatuses = filtered;
    this.totalItems = filtered.length;
  }

  private applyTaskFilters(): void {
    // Start with original source data, not current filtered data
    let filtered = [...this.originalHousekeepingTasks];

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

    // Assignee filter
    if (this.assigneeFilter === 'assigned') {
      filtered = filtered.filter(task => task.assignedToId != null);
    } else if (this.assigneeFilter === 'unassigned') {
      filtered = filtered.filter(task => task.assignedToId == null);
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

  // Task management methods
  createTask(): void {
    if (this.createTaskForm.valid) {
      const formValue = this.createTaskForm.value;
      const request: CreateTaskRequest = {
        title: formValue.title,
        description: formValue.description || `${formValue.taskType} for room ${formValue.roomNumber}`,
        department: 'Housekeeping',
        priority: formValue.priority,
        roomNumber: formValue.roomNumber,
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
            console.error('Error creating housekeeping task:', error);
          }
        });
    }
  }

  updateRoomStatus(room: RoomStatus, newStatus: RoomStatus['status']): void {
    // Update local state immediately for responsive UI
    room.status = newStatus;

    // Update the original room status data to keep statistics accurate
    const originalRoom = this.originalRoomStatuses.find(r => r.roomNumber === room.roomNumber);
    if (originalRoom) {
      originalRoom.status = newStatus;
    }

    this.calculateHousekeepingStats();
    this.cdr.markForCheck(); // Trigger change detection for UI responsiveness

    // Call backend API to persist the change
    this.taskService.updateRoomStatus(room.roomNumber, newStatus)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          console.log(`Successfully updated room ${room.roomNumber} status to ${newStatus}`);
          // Data will be refreshed automatically via the service
        },
        error: (error) => {
          console.error(`Failed to update room ${room.roomNumber} status:`, error);
          // TODO: Show user notification and possibly revert the change
        }
      });
  }

  // Modal methods
  openCreateTaskModal(): void {
    this.modalService.open(this.createTaskModal, { size: 'lg', backdrop: 'static' });
  }

  openRoomDetailModal(room: RoomStatus): void {
    this.selectedRoom = room;
    this.modalService.open(this.roomDetailModal, { size: 'lg', backdrop: 'static' });
  }

  // Utility methods
  getRoomStatusClass(status: RoomStatus['status']): string {
    switch (status) {
      case 'clean': return 'bg-success text-white';
      case 'cleaning': return 'bg-info text-white';
      case 'inspecting': return 'bg-warning text-dark';
      case 'dirty': return 'bg-secondary text-white';
      case 'ooo': return 'bg-danger text-white';
      default: return 'bg-light text-dark';
    }
  }

  getRoomStatusIcon(status: RoomStatus['status']): string {
    switch (status) {
      case 'clean': return 'check-circle';
      case 'cleaning': return 'rotate-cw';
      case 'inspecting': return 'eye';
      case 'dirty': return 'circle';
      case 'ooo': return 'x-circle';
      default: return 'help-circle';
    }
  }

  getPriorityClass(priority: TaskPriority): string {
    switch (priority) {
      case 'Urgent': return 'priority-urgent';
      case 'High': return 'priority-high';
      case 'Medium': return 'priority-medium';
      case 'Low': return 'priority-low';
      default: return 'text-muted';
    }
  }

  getPriorityBadgeClass(priority: TaskPriority): string {
    switch (priority) {
      case 'Urgent': return 'badge priority-urgent';
      case 'High': return 'badge priority-high';
      case 'Medium': return 'badge priority-medium';
      case 'Low': return 'badge priority-low';
      default: return 'badge bg-secondary';
    }
  }

  getStatusBadgeClass(status: TaskStatus): string {
    switch (status) {
      case 'Completed': return 'badge status-completed';
      case 'InProgress': return 'badge status-inprogress';
      case 'Pending': return 'badge status-pending';
      case 'OnHold': return 'badge status-onhold';
      default: return 'badge bg-secondary';
    }
  }

  getTaskIcon(title: string): string {
    const taskType = this.housekeepingTaskTypes.find(t => title.includes(t.label));
    return taskType ? taskType.icon : 'clipboard';
  }

  private getHighestPriority(priorities: TaskPriority[]): TaskPriority {
    const priorityOrder = { 'Urgent': 4, 'High': 3, 'Medium': 2, 'Low': 1 };
    return priorities.reduce((highest, current) =>
      priorityOrder[current] > priorityOrder[highest] ? current : highest, 'Low');
  }

  private calculateEstimatedTime(tasks: StaffTask[]): number {
    const pendingTasks = tasks.filter(t => t.status === 'Pending' || t.status === 'InProgress');
    return pendingTasks.length * 30; // 30 minutes per task estimate
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = 'all';
    this.priorityFilter = 'all';
    this.roomStatusFilter = 'all';
    this.assigneeFilter = 'all';
    this.currentPage = 1;
    this.applyFilters();
  }

  // Pagination
  get paginatedItems(): any[] {
    const items = this.viewMode === 'rooms' ? this.roomStatuses : this.filteredTasks;
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    return items.slice(startIndex, endIndex);
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
    this.loadHousekeepingData();
  }

  // Utility for templates
  taskTrackBy(index: number, task: StaffTask): number {
    return task.id;
  }

  roomTrackBy(index: number, room: RoomStatus): string {
    return room.roomNumber;
  }

  getTaskCountByPriority(priority: string): number {
    // Count tasks by priority level
    return this.filteredTasks.filter(task => task.priority === priority).length;
  }

  getCompletedTasksCount(): number {
    return this.filteredTasks.filter(task => task.status === 'Completed').length;
  }

  get totalTasks(): number {
    return this.filteredTasks.length;
  }

  getTotalActiveTasks(): number {
    return this.filteredTasks.filter(task => task.status !== 'Completed').length;
  }

  isOverdue(task: StaffTask): boolean {
    if (!task.estimatedCompletionTime) return false;
    return new Date(task.estimatedCompletionTime) < new Date() && task.status !== 'Completed';
  }

  getOverdueText(task: StaffTask): string {
    if (!task.estimatedCompletionTime) return '';
    const now = new Date();
    const due = new Date(task.estimatedCompletionTime);
    const diffMs = now.getTime() - due.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
    const diffDays = Math.floor(diffHours / 24);

    if (diffDays > 0) return `${diffDays}d`;
    if (diffHours > 0) return `${diffHours}h`;
    return '< 1h';
  }

  quickUpdateTaskStatus(task: StaffTask, newStatus: string): void {
    const request = { status: newStatus as any };
    this.taskService.updateTask(task.id, request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          task.status = newStatus as any;
          this.loadHousekeepingData();
        },
        error: (error) => {
          console.error('Error updating task status:', error);
        }
      });
  }

  openTaskEditModal(task: StaffTask): void {
    this.selectedTask = task;

    // Populate form with current task data
    this.editTaskForm.patchValue({
      status: task.status,
      priority: task.priority,
      assignedToId: task.assignedToId || null,
      notes: task.notes || '',
      estimatedCompletionTime: task.estimatedCompletionTime
        ? new Date(task.estimatedCompletionTime).toISOString().slice(0, 16)
        : ''
    });

    // Open the modal
    this.modalService.open(this.taskEditModal, {
      size: 'lg',
      centered: true,
      backdrop: 'static'
    });
  }

  saveTaskEdit(): void {
    if (!this.selectedTask || this.editTaskForm.invalid) return;

    const formValue = this.editTaskForm.value;
    const request: UpdateTaskRequest = {
      status: formValue.status,
      priority: formValue.priority,
      assignedToId: formValue.assignedToId || undefined,
      notes: formValue.notes || undefined,
      estimatedCompletionTime: formValue.estimatedCompletionTime
        ? new Date(formValue.estimatedCompletionTime)
        : undefined
    };

    this.taskService.updateTask(this.selectedTask.id, request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.modalService.dismissAll();
          this.refreshData();
          this.cdr.markForCheck();
        },
        error: (error) => {
          console.error('Error updating task:', error);
        }
      });
  }

  // Task Actions
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

    // TODO: Implement message sending functionality
    console.log('Send message to:', task.guestPhone);
  }

  editTask(task: StaffTask): void {
    // TODO: Implement task editing functionality
    console.log('Edit task:', task);
  }
}