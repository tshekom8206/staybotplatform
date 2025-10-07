import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject, of, from } from 'rxjs';
import { map, tap, catchError, switchMap } from 'rxjs/operators';
import { ApiService } from './api.service';
import { IndexedDBService } from './indexed-db.service';
import {
  StaffTask,
  TaskFilter,
  TaskStatistics,
  CreateTaskRequest,
  UpdateTaskRequest,
  Department,
  TaskStatus,
  TaskPriority
} from '../models/task.model';

export interface TaskBulkAction {
  taskIds: number[];
  action: 'assign' | 'updateStatus' | 'delete';
  assignedToId?: number;
  status?: TaskStatus;
}

@Injectable({
  providedIn: 'root'
})
export class TaskService {
  private readonly tasksSubject = new BehaviorSubject<StaffTask[]>([]);
  public readonly tasks$ = this.tasksSubject.asObservable();

  private readonly statisticsSubject = new BehaviorSubject<TaskStatistics | null>(null);
  public readonly statistics$ = this.statisticsSubject.asObservable();

  constructor(
    private apiService: ApiService,
    private indexedDBService: IndexedDBService
  ) {}

  // Get all tasks with optional filtering
  getAllTasks(filter?: TaskFilter): Observable<StaffTask[]> {
    let params = new HttpParams();

    if (filter?.department) params = params.set('department', filter.department);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.priority) params = params.set('priority', filter.priority);
    if (filter?.assignedToId) params = params.set('assignedToId', filter.assignedToId.toString());
    if (filter?.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter?.dateFrom) params = params.set('dateFrom', filter.dateFrom.toISOString());
    if (filter?.dateTo) params = params.set('dateTo', filter.dateTo.toISOString());

    return this.apiService.get<StaffTask[]>('tasks', params)
      .pipe(
        map(tasks => tasks.map(task => this.mapTask(task))),
        tap(tasks => {
          // Save to IndexedDB for offline access
          this.indexedDBService.saveItems('tasks', tasks).catch(err =>
            console.warn('Failed to cache tasks offline:', err)
          );
          this.tasksSubject.next(tasks);
        }),
        catchError(error => {
          console.warn('API call failed, falling back to offline data:', error);
          // Fallback to IndexedDB when API fails (offline)
          return from(this.indexedDBService.getItems<StaffTask>('tasks', 100)).pipe(
            map(tasks => tasks.map(task => this.mapTask(task))),
            tap(tasks => this.tasksSubject.next(tasks))
          );
        })
      );
  }

  // Get tasks assigned to current user
  getMyTasks(filter?: TaskFilter): Observable<StaffTask[]> {
    let params = new HttpParams();

    if (filter?.department) params = params.set('department', filter.department);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.priority) params = params.set('priority', filter.priority);
    if (filter?.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter?.dateFrom) params = params.set('dateFrom', filter.dateFrom.toISOString());
    if (filter?.dateTo) params = params.set('dateTo', filter.dateTo.toISOString());

    return this.apiService.get<StaffTask[]>('tasks/my', params)
      .pipe(
        map(tasks => tasks.map(task => this.mapTask(task))),
        tap(tasks => {
          // Save to IndexedDB for offline access
          this.indexedDBService.saveItems('tasks', tasks).catch(err =>
            console.warn('Failed to cache my tasks offline:', err)
          );
        }),
        catchError(error => {
          console.warn('API call failed for my tasks, falling back to offline data:', error);
          // Fallback to IndexedDB when offline
          return from(this.indexedDBService.getItems<StaffTask>('tasks', 100)).pipe(
            map(tasks => tasks.map(task => this.mapTask(task)))
          );
        })
      );
  }

  // Get tasks by department
  getTasksByDepartment(department: Department, filter?: TaskFilter): Observable<StaffTask[]> {
    const departmentFilter = { ...filter, department };
    return this.getAllTasks(departmentFilter);
  }

  // Get task statistics
  getTaskStatistics(filter?: TaskFilter): Observable<TaskStatistics> {
    let params = new HttpParams();

    if (filter?.dateFrom) params = params.set('dateFrom', filter.dateFrom.toISOString());
    if (filter?.dateTo) params = params.set('dateTo', filter.dateTo.toISOString());

    return this.apiService.get<TaskStatistics>('tasks/statistics', params)
      .pipe(
        tap(stats => this.statisticsSubject.next(stats))
      );
  }

  // Get single task by ID
  getTaskById(id: number): Observable<StaffTask> {
    return this.apiService.get<StaffTask>(`tasks/${id}`)
      .pipe(
        map(task => this.mapTask(task)),
        catchError(error => {
          console.warn(`API call failed for task ${id}, falling back to offline data:`, error);
          // Fallback to IndexedDB when offline
          return from(this.indexedDBService.getItemById<StaffTask>('tasks', id)).pipe(
            map(task => task ? this.mapTask(task) : null),
            switchMap(task => task ? of(task) : this.throwTaskNotFound(id))
          );
        })
      );
  }

  // Create new task
  createTask(request: CreateTaskRequest): Observable<StaffTask> {
    return this.apiService.post<StaffTask>('tasks', request)
      .pipe(
        map(task => ({
          ...task,
          createdAt: new Date(task.createdAt),
          updatedAt: task.updatedAt ? new Date(task.updatedAt) : undefined,
          estimatedCompletionTime: task.estimatedCompletionTime ? new Date(task.estimatedCompletionTime) : undefined,
          completedAt: task.completedAt ? new Date(task.completedAt) : undefined
        })),
        tap(() => this.refreshTasks())
      );
  }

  // Update task
  updateTask(id: number, request: UpdateTaskRequest): Observable<StaffTask> {
    return this.apiService.put<StaffTask>(`tasks/${id}`, request)
      .pipe(
        map(task => ({
          ...task,
          createdAt: new Date(task.createdAt),
          updatedAt: task.updatedAt ? new Date(task.updatedAt) : undefined,
          estimatedCompletionTime: task.estimatedCompletionTime ? new Date(task.estimatedCompletionTime) : undefined,
          completedAt: task.completedAt ? new Date(task.completedAt) : undefined
        })),
        tap(() => this.refreshTasks())
      );
  }

  // Complete task
  completeTask(id: number, completionNotes?: string): Observable<StaffTask> {
    const request: UpdateTaskRequest = {
      status: 'Completed',
      notes: completionNotes
    };
    return this.updateTask(id, request);
  }

  // Assign task
  assignTask(id: number, assignedToId: number): Observable<StaffTask> {
    const request: UpdateTaskRequest = {
      assignedToId
    };
    return this.updateTask(id, request);
  }

  // Delete task
  deleteTask(id: number): Observable<void> {
    return this.apiService.delete<void>(`tasks/${id}`)
      .pipe(
        tap(() => this.refreshTasks())
      );
  }

  // Bulk actions on tasks
  performBulkAction(action: TaskBulkAction): Observable<void> {
    return this.apiService.post<void>('tasks/bulk', action)
      .pipe(
        tap(() => this.refreshTasks())
      );
  }

  // Get tasks for today
  getTodayTasks(): Observable<StaffTask[]> {
    const today = new Date();
    const startOfDay = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const endOfDay = new Date(today.getFullYear(), today.getMonth(), today.getDate(), 23, 59, 59);

    const filter: TaskFilter = {
      dateFrom: startOfDay,
      dateTo: endOfDay
    };

    return this.getAllTasks(filter);
  }

  // Get overdue tasks
  getOverdueTasks(): Observable<StaffTask[]> {
    const now = new Date();
    return this.getAllTasks()
      .pipe(
        map(tasks => tasks.filter(task =>
          task.status !== 'Completed' &&
          task.estimatedCompletionTime &&
          task.estimatedCompletionTime < now
        ))
      );
  }

  // Get urgent tasks
  getUrgentTasks(): Observable<StaffTask[]> {
    const filter: TaskFilter = {
      priority: 'Urgent',
      status: 'Pending'
    };
    return this.getAllTasks(filter);
  }

  // Get task counts by status
  getTaskCountsByStatus(): Observable<{[key in TaskStatus]: number}> {
    return this.getAllTasks()
      .pipe(
        map(tasks => {
          const counts = {
            'Pending': 0,
            'InProgress': 0,
            'Completed': 0,
            'Cancelled': 0,
            'OnHold': 0
          } as {[key in TaskStatus]: number};

          tasks.forEach(task => {
            counts[task.status]++;
          });

          return counts;
        })
      );
  }

  // Request rating for a task
  requestRating(taskId: number): Observable<void> {
    return this.apiService.post<void>(`ratings/request/${taskId}`, {});
  }

  // Save task rating
  saveTaskRating(taskId: number, rating: number, comment?: string): Observable<any> {
    const request = {
      rating: rating,
      comment: comment || ''
    };
    return this.apiService.post<any>(`ratings/tasks/${taskId}`, request);
  }

  // Update room status - this would call backend API when implemented
  updateRoomStatus(roomNumber: string, status: string): Observable<void> {
    // This would call a backend API to update room status
    // For now, implementing as a placeholder that triggers task updates
    const roomStatusUpdate = {
      roomNumber,
      status,
      updatedAt: new Date().toISOString()
    };

    // In a real implementation, this would call:
    // return this.apiService.put<void>(`rooms/${roomNumber}/status`, roomStatusUpdate)

    console.log('Room status update request:', roomStatusUpdate);

    // For now, return success and refresh tasks to sync data
    return of(null as any).pipe(
      tap(() => {
        // Refresh tasks after room status update to sync data
        this.refreshTasks();
      })
    );
  }

  // Private helper methods
  private refreshTasks(): void {
    this.getAllTasks().subscribe();
  }

  private mapTask(task: any): StaffTask {
    return {
      ...task,
      createdAt: new Date(task.createdAt),
      updatedAt: task.updatedAt ? new Date(task.updatedAt) : undefined,
      estimatedCompletionTime: task.estimatedCompletionTime ? new Date(task.estimatedCompletionTime) : undefined,
      completedAt: task.completedAt ? new Date(task.completedAt) : undefined
    };
  }

  private throwTaskNotFound(id: number): Observable<never> {
    throw new Error(`Task ${id} not found in offline storage`);
  }

}