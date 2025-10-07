import { Routes } from '@angular/router';

export default [
  {
    path: '',
    redirectTo: 'compose',
    pathMatch: 'full'
  },
  {
    path: 'compose',
    loadComponent: () => import('./compose/compose.component').then(m => m.ComposeComponent),
    title: 'Send Message - Hostr Admin'
  },
  {
    path: 'emergency',
    loadComponent: () => import('./emergency/emergency.component').then(m => m.EmergencyComponent),
    title: 'Emergency Alert - Hostr Admin'
  },
  {
    path: 'templates',
    loadComponent: () => import('./templates/templates.component').then(m => m.TemplatesComponent),
    title: 'Message Templates - Hostr Admin'
  },
  {
    path: 'history',
    loadComponent: () => import('./history/history.component').then(m => m.HistoryComponent),
    title: 'Broadcast History - Hostr Admin'
  },
  {
    path: '**',
    redirectTo: 'compose',
    pathMatch: 'full'
  }
] as Routes;