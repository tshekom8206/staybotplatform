import { Routes } from '@angular/router';

export default [
  {
    path: '',
    redirectTo: 'active',
    pathMatch: 'full'
  },
  {
    path: 'active',
    loadComponent: () => import('./active/active.component').then(c => c.ActiveConversationsComponent),
    title: 'Active Conversations - Hostr Admin'
  },
  {
    path: 'assignments',
    loadComponent: () => import('./assignments/assignments.component').then(c => c.ConversationAssignmentsComponent),
    title: 'Agent Assignments - Hostr Admin'
  },
  {
    path: 'transfers',
    loadComponent: () => import('./transfers/transfers.component').then(c => c.TransferQueueComponent),
    title: 'Transfer Queue - Hostr Admin'
  },
  {
    path: 'history',
    loadComponent: () => import('./history/history.component').then(c => c.ConversationHistoryComponent),
    title: 'Conversation History - Hostr Admin'
  }
] as Routes;