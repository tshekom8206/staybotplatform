import { Routes } from '@angular/router';

export const LostAndFoundRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./lost-and-found.component').then(c => c.LostAndFoundComponent),
    title: 'Lost & Found - Hostr Admin'
  }
];

export default LostAndFoundRoutes;
