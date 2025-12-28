import { Routes } from '@angular/router';
import { BaseComponent } from './views/layout/base/base.component';

export const routes: Routes = [
  {
    path: '',
    component: BaseComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./views/pages/home/home.component').then(c => c.HomeComponent)
      },
      {
        path: 'food-drinks',
        loadComponent: () => import('./views/pages/food-drinks/food-drinks.component').then(c => c.FoodDrinksComponent)
      },
      {
        path: 'maintenance',
        loadComponent: () => import('./views/pages/maintenance/maintenance.component').then(c => c.MaintenanceComponent)
      },
      {
        path: 'amenities',
        loadComponent: () => import('./views/pages/amenities/amenities.component').then(c => c.AmenitiesComponent)
      },
      {
        path: 'lost-found',
        loadComponent: () => import('./views/pages/lost-found/lost-found.component').then(c => c.LostFoundComponent)
      },
      {
        path: 'rate-us',
        loadComponent: () => import('./views/pages/rate-us/rate-us.component').then(c => c.RateUsComponent)
      },
      {
        path: 'our-promise',
        loadComponent: () => import('./views/pages/our-promise/our-promise.component').then(c => c.OurPromiseComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
