import { inject } from '@angular/core';
import { HttpInterceptorFn, HttpRequest, HttpErrorResponse, HttpHandlerFn } from '@angular/common/http';
import { throwError, BehaviorSubject } from 'rxjs';
import { catchError, filter, take, switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

let isRefreshing = false;
let refreshTokenSubject: BehaviorSubject<any> = new BehaviorSubject<any>(null);

export const AuthInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);

  // Add auth token and tenant headers to request
  const authRequest = addToken(request, authService.getToken(), authService);

  return next(authRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        return handle401Error(authRequest, next, authService);
      }
      return throwError(() => error);
    })
  );
};

function addToken(request: HttpRequest<any>, token: string | null, authService: AuthService): HttpRequest<any> {
  const headers: { [name: string]: string } = {};

  // Only set Content-Type for requests with body (POST, PUT, PATCH)
  if (request.method === 'POST' || request.method === 'PUT' || request.method === 'PATCH') {
    headers['Content-Type'] = 'application/json';
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  // Add tenant header from authenticated user
  const currentTenant = authService.currentTenantValue;
  if (currentTenant?.slug) {
    headers['X-Tenant'] = currentTenant.slug;
  } else {
    // Fallback to default tenant for development
    headers['X-Tenant'] = 'panoramaview';
  }

  console.log('[AuthInterceptor] Request URL:', request.url);
  console.log('[AuthInterceptor] Headers being added:', headers);
  console.log('[AuthInterceptor] Has token:', !!token);

  return request.clone({
    setHeaders: headers
  });
}

function handle401Error(request: HttpRequest<any>, next: HttpHandlerFn, authService: AuthService) {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return authService.refreshToken().pipe(
      switchMap((token: string) => {
        isRefreshing = false;
        refreshTokenSubject.next(token);
        return next(addToken(request, token, authService));
      }),
      catchError((error) => {
        isRefreshing = false;
        authService.logout();
        return throwError(() => error);
      })
    );
  } else {
    return refreshTokenSubject.pipe(
      filter(token => token != null),
      take(1),
      switchMap(token => {
        return next(addToken(request, token, authService));
      })
    );
  }
}