import { Routes } from '@angular/router';

export default [
  {
    path: '',
    redirectTo: 'analytics',
    pathMatch: 'full'
  },
  {
    path: 'analytics',
    loadComponent: () => import('./analytics/analytics.component').then(c => c.AnalyticsComponent),
    title: 'Analytics Dashboard - Hostr Admin'
  },
  {
    path: 'tasks',
    loadComponent: () => import('./tasks/tasks.component').then(c => c.TasksComponent),
    title: 'Task Performance Report - Hostr Admin'
  },
  {
    path: 'satisfaction',
    loadComponent: () => import('./satisfaction/satisfaction.component').then(c => c.SatisfactionComponent),
    title: 'Guest Satisfaction Report - Hostr Admin'
  },
  {
    path: 'usage',
    loadComponent: () => import('./usage/usage.component').then(c => c.UsageComponent),
    title: 'Service Usage Report - Hostr Admin'
  }
] as Routes;