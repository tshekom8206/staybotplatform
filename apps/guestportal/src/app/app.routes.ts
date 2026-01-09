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
        path: 'housekeeping',
        loadComponent: () => import('./views/pages/housekeeping/housekeeping.component').then(c => c.HousekeepingComponent)
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
      },
      {
        path: 'services',
        loadComponent: () => import('./views/pages/services/services.component').then(c => c.ServicesComponent)
      },
      {
        path: 'house-rules',
        loadComponent: () => import('./views/pages/house-rules/house-rules.component').then(c => c.HouseRulesComponent)
      },
      {
        path: 'prepare',
        loadComponent: () => import('./views/pages/prepare/prepare.component').then(c => c.PrepareComponent)
      },
      {
        path: 'feedback',
        loadComponent: () => import('./views/pages/feedback/feedback.component').then(c => c.FeedbackComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
