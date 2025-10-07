import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

export const ErrorInterceptor: HttpInterceptorFn = (request, next) => {
  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
        let errorMessage = 'An error occurred';

        if (error.error instanceof ErrorEvent) {
          // Client-side error
          errorMessage = `Error: ${error.error.message}`;
        } else {
          // Server-side error
          switch (error.status) {
            case 400:
              errorMessage = error.error?.error || error.error?.message || 'Bad Request';
              break;
            case 401:
              errorMessage = 'Unauthorized - Please login again';
              break;
            case 403:
              errorMessage = 'Forbidden - You do not have permission to access this resource';
              break;
            case 404:
              errorMessage = 'Resource not found';
              break;
            case 500:
              errorMessage = 'Internal server error - Please try again later';
              break;
            case 503:
              errorMessage = 'Service unavailable - Please try again later';
              break;
            default:
              errorMessage = error.error?.error || error.error?.message || `Error Code: ${error.status}`;
              break;
          }
        }

        console.error('HTTP Error:', errorMessage, error);
        return throwError(() => new Error(errorMessage));
      })
    );
};