import { Injectable, inject } from '@angular/core';
import { Observable, throwError, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';

export interface ResponseTemplate {
  id: number;
  templateKey: string;
  category: string;
  language: string;
  template: string;
  isActive: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
}

export interface ResponseVariable {
  id: number;
  variableName: string;
  variableValue: string;
  category?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateTemplateRequest {
  templateKey: string;
  category: string;
  language?: string;
  template: string;
  isActive?: boolean;
  priority?: number;
}

export interface UpdateTemplateRequest {
  templateKey: string;
  category: string;
  language?: string;
  template: string;
  isActive?: boolean;
  priority?: number;
}

export interface CreateVariableRequest {
  variableName: string;
  variableValue: string;
  category?: string;
  isActive?: boolean;
}

export interface TemplateSearchParams {
  category?: string;
  language?: string;
  isActive?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface TemplateListResponse {
  templates: ResponseTemplate[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ProcessedTemplate {
  content: string;
  usedVariables: { [key: string]: string };
  missingVariables: string[];
}

@Injectable({
  providedIn: 'root'
})
export class TemplateService {
  private apiService = inject(ApiService);
  private authService = inject(AuthService);

  /**
   * Get templates with optional filtering and pagination
   */
  getTemplates(params: TemplateSearchParams = {}): Observable<TemplateListResponse> {
    const queryParams = new URLSearchParams();

    if (params.category) queryParams.append('category', params.category);
    if (params.language) queryParams.append('language', params.language);
    if (params.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());
    if (params.search) queryParams.append('search', params.search);
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());

    const url = `responsetemplate${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;

    return this.apiService.get<{success: boolean, data: TemplateListResponse}>(url)
      .pipe(
        map(response => response.data || {templates: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0}),
        catchError(error => {
          console.error('Error loading templates:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get a specific template by ID
   */
  getTemplate(id: number): Observable<ResponseTemplate> {
    return this.apiService.get<ResponseTemplate>(`responsetemplate/${id}`)
      .pipe(
        map(response => response),
        catchError(error => {
          console.error('Error loading template:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Create a new template
   */
  createTemplate(template: CreateTemplateRequest): Observable<ResponseTemplate> {
    return this.apiService.post<ResponseTemplate>('responsetemplate', template)
      .pipe(
        map(response => response),
        catchError(error => {
          console.error('Error creating template:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Update an existing template
   */
  updateTemplate(id: number, template: UpdateTemplateRequest): Observable<ResponseTemplate> {
    return this.apiService.put<ResponseTemplate>(`responsetemplate/${id}`, template)
      .pipe(
        map(response => response),
        catchError(error => {
          console.error('Error updating template:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Delete a template
   */
  deleteTemplate(id: number): Observable<any> {
    return this.apiService.delete(`responsetemplate/${id}`)
      .pipe(
        map(response => response),
        catchError(error => {
          console.error('Error deleting template:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Get template categories
   */
  getCategories(): Observable<string[]> {
    return this.apiService.get<{success: boolean, data: string[]}>('responsetemplate/categories')
      .pipe(
        map(response => response.data || []),
        catchError(error => {
          console.error('Error loading categories:', error);
          // Return empty array as fallback
          return of([]);
        })
      );
  }

  /**
   * Get variables with optional category filter
   */
  getVariables(category?: string): Observable<ResponseVariable[]> {
    const url = category ? `responsetemplate/variables?category=${category}` : 'responsetemplate/variables';

    return this.apiService.get<{success: boolean, data: ResponseVariable[]}>(url)
      .pipe(
        map(response => response.data || []),
        catchError(error => {
          console.error('Error loading variables:', error);
          return of([]);
        })
      );
  }

  /**
   * Create or update a variable
   */
  createOrUpdateVariable(variable: CreateVariableRequest): Observable<any> {
    return this.apiService.post('responsetemplate/variables', variable)
      .pipe(
        map(response => response),
        catchError(error => {
          console.error('Error creating/updating variable:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Delete a variable
   */
  deleteVariable(id: number): Observable<any> {
    return this.apiService.delete(`responsetemplate/variables/${id}`)
      .pipe(
        map(response => response),
        catchError(error => {
          console.error('Error deleting variable:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * Preview template with current variables
   */
  previewTemplate(templateContent: string, variables?: { [key: string]: string }): ProcessedTemplate {
    let processedContent = templateContent;
    const usedVariables: { [key: string]: string } = {};
    const missingVariables: string[] = [];

    // Find all variables in the template (format: {{variable_name}})
    const variablePattern = /\{\{([^}]+)\}\}/g;
    let match;

    while ((match = variablePattern.exec(templateContent)) !== null) {
      const variableName = match[1].trim();

      if (variables && variables[variableName]) {
        processedContent = processedContent.replace(match[0], variables[variableName]);
        usedVariables[variableName] = variables[variableName];
      } else {
        missingVariables.push(variableName);
      }
    }

    return {
      content: processedContent,
      usedVariables,
      missingVariables: [...new Set(missingVariables)] // Remove duplicates
    };
  }

  /**
   * Get available template keys (predefined keys from the backend)
   */
  getAvailableTemplateKeys(): string[] {
    // These should match the ResponseTemplateKeys constants from the backend
    return [
      // Service requests
      'service_request_laundry_available',
      'service_request_laundry_unavailable',
      'service_request_housekeeping_available',
      'service_request_housekeeping_unavailable',
      'service_request_room_service_available',
      'service_request_room_service_unavailable',

      // Contextual responses
      'acknowledgment_positive',
      'acknowledgment_negative',
      'anything_else_positive',
      'anything_else_negative',
      'front_desk_connection',
      'temperature_complaint',

      // WiFi support
      'wifi_troubleshooting_followup',
      'wifi_technical_support',
      'wifi_working_confirmation',
      'wifi_still_not_working',

      // Emergency and maintenance
      'emergency_detected',
      'maintenance_urgent',
      'maintenance_standard',

      // Menu responses
      'menu_price_inquiry',
      'menu_full_request',
      'menu_more_details',
      'menu_default',

      // Fallback responses
      'fallback_clarification',
      'fallback_general_help',

      // Time-based responses
      'laundry_schedule_confirmation',
      'housekeeping_schedule_confirmation',
      'food_delivery_confirmation',

      // Item requests
      'towel_delivery_confirmation',
      'charger_request',
      'charger_delivery_confirmation',
      'iron_delivery_confirmation',
      'toilet_paper_delivery_confirmation',
      'amenity_delivery_confirmation'
    ];
  }

  /**
   * Get available variable names (predefined variables from the backend)
   */
  getAvailableVariableNames(): { [category: string]: string[] } {
    return {
      'Hotel/Brand Info': [
        'hotel_name',
        'brand_voice',
        'phone',
        'email',
        'website'
      ],
      'Dynamic Context': [
        'room_number',
        'guest_name',
        'time_of_day',
        'current_time'
      ],
      'Service Context': [
        'service_name',
        'service_status',
        'availability_message',
        'quantity',
        'time_message',
        'charger_type',
        'amenity_name'
      ]
    };
  }

  /**
   * Get default categories for templates
   */
  getDefaultCategories(): string[] {
    return [
      'Service Requests',
      'Contextual Responses',
      'WiFi Support',
      'Emergency',
      'Maintenance',
      'Menu',
      'Fallback',
      'Time-based',
      'Item Requests'
    ];
  }

  /**
   * Get available languages
   */
  getAvailableLanguages(): { code: string, name: string }[] {
    return [
      { code: 'en', name: 'English' },
      { code: 'es', name: 'Spanish' },
      { code: 'fr', name: 'French' },
      { code: 'de', name: 'German' },
      { code: 'it', name: 'Italian' },
      { code: 'pt', name: 'Portuguese' },
      { code: 'af', name: 'Afrikaans' },
      { code: 'zu', name: 'Zulu' },
      { code: 'xh', name: 'Xhosa' }
    ];
  }
}