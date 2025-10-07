export interface StaffTask {
  id: number;
  tenantId: number;
  conversationId?: number;
  requestItemId?: number;
  bookingId?: number;
  title: string;
  description: string;
  department: Department;
  priority: TaskPriority;
  status: TaskStatus;
  assignedToId?: number;
  assignedTo?: User;
  roomNumber?: string;
  guestName?: string;
  guestPhone?: string;
  quantity?: number;
  estimatedCompletionTime?: Date;
  completedAt?: Date;
  completedBy?: number;
  notes?: string;
  createdAt: Date;
  updatedAt?: Date;
  requestItem?: RequestItem;
  conversation?: Conversation;
}

export type Department = 'Housekeeping' | 'Maintenance' | 'FrontDesk' | 'Concierge' | 'FoodService' | 'General';

export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Urgent';

export type TaskStatus = 'Pending' | 'InProgress' | 'Completed' | 'Cancelled' | 'OnHold';

export interface RequestItem {
  id: number;
  tenantId: number;
  name: string;
  category: string;
  department: Department;
  llmVisibleName: string;
  estimatedTime?: number;
  requiresQuantity: boolean;
  defaultQuantityLimit?: number;
  notesForStaff?: string;
  isActive: boolean;
  isUrgent: boolean;
  displayOrder: number;
}

export interface CreateTaskRequest {
  title: string;
  description: string;
  department: Department;
  priority: TaskPriority;
  roomNumber?: string;
  guestName?: string;
  assignedToId?: number;
  estimatedCompletionTime?: Date;
  notes?: string;
}

export interface UpdateTaskRequest {
  status?: TaskStatus;
  priority?: TaskPriority;
  assignedToId?: number;
  notes?: string;
  estimatedCompletionTime?: Date;
}

export interface TaskFilter {
  department?: Department;
  status?: TaskStatus;
  priority?: TaskPriority;
  assignedToId?: number;
  dateFrom?: Date;
  dateTo?: Date;
  searchTerm?: string;
}

export interface TaskStatistics {
  totalTasks: number;
  pendingTasks: number;
  inProgressTasks: number;
  completedTasks: number;
  averageCompletionTime: number;
  tasksByDepartment: { [key: string]: number };
  tasksByPriority: { [key: string]: number };
}

export interface User {
  id: number;
  userName: string;
  email: string;
}

export interface Conversation {
  id: number;
  waUserPhone: string;
}