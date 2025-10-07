import { Routes } from '@angular/router';

export const TasksRoutes: Routes = [
  {
    path: '',
    redirectTo: 'all',
    pathMatch: 'full'
  },
  {
    path: 'all',
    loadComponent: () => import('./all-tasks/all-tasks.component').then(c => c.AllTasksComponent),
    title: 'All Tasks - Hostr Admin'
  },
  {
    path: 'my',
    loadComponent: () => import('./my-tasks/my-tasks.component').then(c => c.MyTasksComponent),
    title: 'My Tasks - Hostr Admin'
  },
  {
    path: 'housekeeping',
    loadComponent: () => import('./housekeeping/housekeeping.component').then(c => c.HousekeepingComponent),
    title: 'Housekeeping Tasks - Hostr Admin'
  },
  {
    path: 'maintenance',
    loadComponent: () => import('./maintenance/maintenance.component').then(c => c.MaintenanceComponent),
    title: 'Maintenance Tasks - Hostr Admin'
  },
  {
    path: 'frontdesk',
    loadComponent: () => import('./frontdesk/frontdesk.component').then(c => c.FrontdeskComponent),
    title: 'Front Desk Tasks - Hostr Admin'
  }
];

export default TasksRoutes;