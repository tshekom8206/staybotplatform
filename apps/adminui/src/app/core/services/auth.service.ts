import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { map, tap, catchError } from 'rxjs/operators';
import { ApiService, ApiResponse } from './api.service';
import { StorageService } from './storage.service';
import { User, LoginRequest, LoginResponse, AuthToken, Tenant } from '../models/user.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private currentUserSubject: BehaviorSubject<User | null>;
  private currentTenantSubject: BehaviorSubject<Tenant | null>;
  public currentUser: Observable<User | null>;
  public currentTenant: Observable<Tenant | null>;

  private tokenKey = 'auth_token';
  private refreshTokenKey = 'refresh_token';
  private userKey = 'current_user';
  private tenantKey = 'current_tenant';

  constructor(
    private apiService: ApiService,
    private storageService: StorageService,
    private router: Router
  ) {
    const storedUser = this.storageService.getItem(this.userKey);
    const storedTenant = this.storageService.getItem(this.tenantKey);

    this.currentUserSubject = new BehaviorSubject<User | null>(storedUser as User | null);
    this.currentTenantSubject = new BehaviorSubject<Tenant | null>(storedTenant as Tenant | null);
    this.currentUser = this.currentUserSubject.asObservable();
    this.currentTenant = this.currentTenantSubject.asObservable();
  }

  public get currentUserValue(): User | null {
    return this.currentUserSubject.value;
  }

  public get currentTenantValue(): Tenant | null {
    return this.currentTenantSubject.value;
  }

  public get isAuthenticated(): boolean {
    return !!this.getToken();
  }

  /**
   * Login user
   */
  login(credentials: LoginRequest): Observable<LoginResponse> {
    return this.apiService.post<ApiResponse<LoginResponse>>('/auth/login', credentials).pipe(
      map(response => {
        if (response.success && response.data) {
          const loginData = response.data;

          // Store tokens
          this.storageService.setItem(this.tokenKey, loginData.token);
          if (loginData.refreshToken) {
            this.storageService.setItem(this.refreshTokenKey, loginData.refreshToken);
          }

          // Store user and tenant
          this.storageService.setItem(this.userKey, loginData.user);
          this.storageService.setItem(this.tenantKey, loginData.tenant);

          // Update subjects
          this.currentUserSubject.next(loginData.user);
          this.currentTenantSubject.next(loginData.tenant as any);

          // Set token expiration - use default if not provided
          const expiresAt = new Date();
          const expiresInSeconds = loginData.expiresIn || (60 * 60); // Default to 1 hour if not provided
          expiresAt.setSeconds(expiresAt.getSeconds() + expiresInSeconds);
          this.storageService.setItem('token_expires_at', expiresAt.toISOString());

          return loginData;
        }
        throw new Error(response.error || 'Login failed');
      })
    );
  }

  /**
   * Logout user
   */
  logout(): void {
    // Clear storage
    this.storageService.removeItem(this.tokenKey);
    this.storageService.removeItem(this.refreshTokenKey);
    this.storageService.removeItem(this.userKey);
    this.storageService.removeItem(this.tenantKey);
    this.storageService.removeItem('token_expires_at');

    // Clear subjects
    this.currentUserSubject.next(null);
    this.currentTenantSubject.next(null);

    // Navigate to login
    this.router.navigate(['/auth/login']);
  }

  /**
   * Refresh token
   */
  refreshToken(): Observable<string> {
    const refreshToken = this.getRefreshToken();

    if (!refreshToken) {
      this.logout();
      return throwError(() => new Error('No refresh token available'));
    }

    return this.apiService.post<ApiResponse<LoginResponse>>('/auth/refresh', { refreshToken }).pipe(
      map(response => {
        if (response.success && response.data) {
          const loginData = response.data;

          // Update tokens
          this.storageService.setItem(this.tokenKey, loginData.token);
          this.storageService.setItem(this.refreshTokenKey, loginData.refreshToken);

          // Update expiration - use default if not provided
          const expiresAt = new Date();
          const expiresInSeconds = loginData.expiresIn || (60 * 60); // Default to 1 hour if not provided
          expiresAt.setSeconds(expiresAt.getSeconds() + expiresInSeconds);
          this.storageService.setItem('token_expires_at', expiresAt.toISOString());

          return loginData.token;
        }
        throw new Error('Token refresh failed');
      }),
      catchError(error => {
        this.logout();
        return throwError(() => error);
      })
    );
  }

  /**
   * Change password
   */
  changePassword(currentPassword: string, newPassword: string): Observable<any> {
    return this.apiService.post('/auth/change-password', {
      currentPassword,
      newPassword
    });
  }

  /**
   * Reset password request
   */
  requestPasswordReset(email: string): Observable<any> {
    return this.apiService.post('/auth/forgot-password', { email });
  }

  /**
   * Get stored token
   */
  getToken(): string | null {
    return this.storageService.getItem(this.tokenKey);
  }

  /**
   * Get refresh token
   */
  getRefreshToken(): string | null {
    return this.storageService.getItem(this.refreshTokenKey);
  }

  /**
   * Check if token is expired
   */
  isTokenExpired(): boolean {
    const expiresAt = this.storageService.getItem('token_expires_at');
    if (!expiresAt) return true;

    const expiration = new Date(expiresAt as string);
    return expiration <= new Date();
  }

  /**
   * Get user role for current tenant
   */
  getUserRole(): string | null {
    const user = this.currentUserValue;
    const tenant = this.currentTenantValue;

    if (!user || !tenant) return null;

    // In a real implementation, this would be part of the user object
    // or fetched from the API
    return 'Manager'; // Placeholder
  }

  /**
   * Check if user has specific role
   */
  hasRole(role: string): boolean {
    const userRole = this.getUserRole();
    return userRole === role;
  }

  /**
   * Check if user has any of the specified roles
   */
  hasAnyRole(roles: string[]): boolean {
    const userRole = this.getUserRole();
    return userRole ? roles.includes(userRole) : false;
  }
}