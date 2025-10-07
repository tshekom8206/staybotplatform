import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { catchError, map, of } from 'rxjs';

export const authGuard: CanActivateFn = (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  // Check if user is authenticated and token is not expired
  if (authService.isAuthenticated && !authService.isTokenExpired()) {
    return true;
  }

  // Check if we have a refresh token and try to refresh
  if (authService.getRefreshToken()) {
    return authService.refreshToken().pipe(
      map(() => true),
      catchError(() => {
        // Refresh failed, redirect to login
        router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url.split('?')[0] } });
        return of(false);
      })
    );
  }

  // No valid authentication, redirect to login
  router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url.split('?')[0] } });
  return false;
};
