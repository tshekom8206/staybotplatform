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
  },
  {
    path: 'guest-portal',
    loadComponent: () => import('./guest-portal-analytics/guest-portal-analytics.component').then(c => c.GuestPortalAnalyticsComponent),
    title: 'Guest Portal Analytics - Hostr Admin'
  },
  {
    path: 'hotelier-insights',
    loadComponent: () => import('./hotelier-insights/hotelier-insights.component').then(c => c.HotelierInsightsComponent),
    title: 'Hotelier Insights - Hostr Admin'
  }
] as Routes;