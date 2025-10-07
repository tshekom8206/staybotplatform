import { Routes } from '@angular/router';

export default [
  {
    path: '',
    redirectTo: 'staff',
    pathMatch: 'full'
  },
  {
    path: 'staff',
    loadComponent: () => import('./staff/staff.component').then(c => c.StaffComponent),
    title: 'Staff Management - Hostr Admin'
  },
  {
    path: 'agents',
    loadComponent: () => import('./agents/agents.component').then(c => c.AgentsComponent),
    title: 'Agent Dashboard - Hostr Admin'
  },
  {
    path: 'roles',
    loadComponent: () => import('./roles/roles.component').then(c => c.RolesComponent),
    title: 'Roles & Permissions - Hostr Admin'
  },
  {
    path: 'activity',
    loadComponent: () => import('./activity/activity.component').then(c => c.ActivityComponent),
    title: 'User Activity - Hostr Admin'
  }
] as Routes;