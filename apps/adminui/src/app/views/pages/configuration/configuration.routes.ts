import { Routes } from '@angular/router';

export default [
  {
    path: '',
    redirectTo: 'hotel-info',
    pathMatch: 'full'
  },
  {
    path: 'hotel-info',
    loadComponent: () => import('./hotel-info/hotel-info.component').then(c => c.HotelInfoComponent),
    title: 'Hotel Information - Hostr Admin'
  },
  {
    path: 'services',
    loadComponent: () => import('./services/services.component').then(c => c.ServicesComponent),
    title: 'Services & Amenities - Hostr Admin'
  },
  {
    path: 'faqs',
    loadComponent: () => import('./faqs/faqs.component').then(c => c.FAQsComponent),
    title: 'FAQ Management - Hostr Admin'
  },
  {
    path: 'menu',
    loadComponent: () => import('./menu/menu.component').then(c => c.MenuComponent),
    title: 'Menu Management - Hostr Admin'
  },
  {
    path: 'emergency',
    loadComponent: () => import('./emergency/emergency.component').then(c => c.EmergencyComponent),
    title: 'Emergency Management - Hostr Admin'
  },
  {
    path: 'templates',
    loadComponent: () => import('./template-manager/template-manager.component').then(c => c.TemplateManagerComponent),
    title: 'Template Manager - Hostr Admin'
  }
] as Routes;