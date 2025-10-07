import { Routes } from '@angular/router';

export default [
  {
    path: '',
    redirectTo: 'conversations',
    pathMatch: 'full'
  },
  {
    path: 'conversations',
    loadComponent: () => import('./conversations/conversations.component').then(c => c.ConversationsComponent),
    title: 'Active Conversations'
  },
  {
    path: 'bookings',
    loadComponent: () => import('./bookings/bookings.component').then(c => c.BookingsComponent),
    title: 'Bookings Management'
  },
  {
    path: 'checkins',
    loadComponent: () => import('./checkins/checkins.component').then(c => c.CheckinsComponent),
    title: 'Check-ins Today'
  },
  {
    path: 'history',
    loadComponent: () => import('./history/history.component').then(c => c.HistoryComponent),
    title: 'Guest History'
  },
  {
    path: 'interaction/:id',
    loadComponent: () => import('./interaction-detail/interaction-detail.component').then(c => c.InteractionDetailComponent),
    title: 'Interaction Details'
  }
] as Routes;
