import { Routes } from '@angular/router';

export default [
  {
    path: '',
    redirectTo: 'business-impact',
    pathMatch: 'full'
  },
  {
    path: 'business-impact',
    loadComponent: () => import('./business-impact/business-impact.component').then(c => c.BusinessImpactComponent)
  }
] as Routes;