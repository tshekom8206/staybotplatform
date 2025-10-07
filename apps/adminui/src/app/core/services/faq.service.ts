import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';

export interface FAQ {
  id: number;
  question: string;
  answer: string;
  language: string;
  tags: string[];
  updatedAt: Date;
}

export interface CreateFAQRequest {
  question: string;
  answer: string;
  language?: string;
  tags?: string[];
}

export interface UpdateFAQRequest {
  question: string;
  answer: string;
  language?: string;
  tags?: string[];
}

export interface FAQStats {
  total: number;
  byLanguage: Array<{language: string, count: number}>;
  uniqueTags: string[];
}

@Injectable({
  providedIn: 'root'
})
export class FAQService {
  private apiService = inject(ApiService);
  private authService = inject(AuthService);

  /**
   * Get all FAQs for the current tenant
   */
  getFAQs(): Observable<FAQ[]> {
    return this.apiService.get<{ faqs: FAQ[] }>('faq')
      .pipe(
        map(response => response.faqs.map(faq => ({
          ...faq,
          updatedAt: new Date(faq.updatedAt)
        }))),
        catchError(error => {
          console.error('Error loading FAQs:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get a specific FAQ by ID
   */
  getFAQ(id: number): Observable<FAQ> {
    return this.apiService.get<{ faq: FAQ }>(`faq/${id}`)
      .pipe(
        map(response => ({
          ...response.faq,
          updatedAt: new Date(response.faq.updatedAt)
        })),
        catchError(error => {
          console.error('Error loading FAQ:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Create a new FAQ
   */
  createFAQ(faq: CreateFAQRequest): Observable<FAQ> {
    return this.apiService.post<{ faq: FAQ }>('faq', faq)
      .pipe(
        map(response => ({
          ...response.faq,
          updatedAt: new Date(response.faq.updatedAt)
        })),
        catchError(error => {
          console.error('Error creating FAQ:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Update an existing FAQ
   */
  updateFAQ(id: number, faq: UpdateFAQRequest): Observable<FAQ> {
    return this.apiService.put<{ faq: FAQ }>(`faq/${id}`, faq)
      .pipe(
        map(response => ({
          ...response.faq,
          updatedAt: new Date(response.faq.updatedAt)
        })),
        catchError(error => {
          console.error('Error updating FAQ:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Delete an FAQ
   */
  deleteFAQ(id: number): Observable<void> {
    return this.apiService.delete<void>(`faq/${id}`)
      .pipe(
        catchError(error => {
          console.error('Error deleting FAQ:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get FAQ statistics
   */
  getFAQStats(): Observable<FAQStats> {
    return this.apiService.get<FAQStats>('faq/stats')
      .pipe(
        catchError(error => {
          console.error('Error loading FAQ statistics:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Search FAQs
   */
  searchFAQs(query: string, language?: string): Observable<{faqs: FAQ[], query: string, language?: string}> {
    let queryParams = `?query=${encodeURIComponent(query)}`;
    if (language) {
      queryParams += `&language=${encodeURIComponent(language)}`;
    }

    return this.apiService.get<{faqs: FAQ[], query: string, language?: string}>(`faq/search${queryParams}`)
      .pipe(
        map(response => ({
          ...response,
          faqs: response.faqs.map(faq => ({
            ...faq,
            updatedAt: new Date(faq.updatedAt)
          }))
        })),
        catchError(error => {
          console.error('Error searching FAQs:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get available languages (hardcoded list)
   */
  getAvailableLanguages(): Array<{code: string, name: string}> {
    return [
      { code: 'en', name: 'English' },
      { code: 'af', name: 'Afrikaans' },
      { code: 'zu', name: 'Zulu' },
      { code: 'xh', name: 'Xhosa' },
      { code: 'st', name: 'Sotho' },
      { code: 'tn', name: 'Tswana' },
      { code: 'ss', name: 'Swati' },
      { code: 've', name: 'Venda' },
      { code: 'ts', name: 'Tsonga' },
      { code: 'nr', name: 'Ndebele' },
      { code: 'nso', name: 'Northern Sotho' }
    ];
  }

  /**
   * Get predefined FAQ categories/tags
   */
  getCommonTags(): string[] {
    return [
      'check-in',
      'check-out',
      'amenities',
      'wifi',
      'parking',
      'breakfast',
      'tours',
      'transportation',
      'local-attractions',
      'policies',
      'payment',
      'cancellation',
      'rooms',
      'facilities',
      'services',
      'emergency',
      'contact',
      'location',
      'activities',
      'dining'
    ];
  }
}