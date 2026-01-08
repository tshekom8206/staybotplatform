import { Routes } from '@angular/router';

export const BusinessRulesRoutes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/dashboard/dashboard.component').then(c => c.BusinessRulesDashboardComponent),
    title: 'Business Rules Dashboard - Hostr Admin'
  },
  {
    path: 'services',
    loadComponent: () => import('./pages/services-list/services-list.component').then(c => c.ServicesListComponent),
    title: 'Service Business Rules - Hostr Admin'
  },
  {
    path: 'request-items',
    loadComponent: () => import('./pages/services-list/services-list.component').then(c => c.ServicesListComponent),
    title: 'Request Item Rules - Hostr Admin',
    data: { type: 'request-items' }
  },
  {
    path: 'upselling',
    loadComponent: () => import('./pages/upselling/upselling.component').then(c => c.UpsellingComponent),
    title: 'Upselling Management - Hostr Admin'
  },
  {
    path: 'weather-upselling',
    loadComponent: () => import('./pages/weather-upselling/weather-upselling.component').then(c => c.WeatherUpsellingComponent),
    title: 'Weather-Based Upselling - Hostr Admin'
  },
  {
    path: 'audit-log',
    loadComponent: () => import('./pages/audit-log/audit-log.component').then(c => c.AuditLogComponent),
    title: 'Audit Log - Hostr Admin'
  }
];

export default BusinessRulesRoutes;
