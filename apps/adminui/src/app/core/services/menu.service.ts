import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface MenuCategory {
  id: number;
  name: string;
  description?: string;
  mealType: string;
  displayOrder: number;
  isActive: boolean;
  updatedAt: string;
  menuItems?: MenuItem[];
  itemCount?: number;
}

export interface MenuItem {
  id: number;
  menuCategoryId: number;
  name: string;
  description: string;
  priceCents: number;
  currency: string;
  formattedPrice: string;
  allergens?: string;
  mealType: string;
  isVegetarian: boolean;
  isVegan: boolean;
  isGlutenFree: boolean;
  isSpicy: boolean;
  isAvailable: boolean;
  isSpecial: boolean;
  tags: string[];
  updatedAt: string;
  menuCategory?: MenuCategory;
}

export interface MenuSpecial {
  id: number;
  menuItemId?: number;
  title: string;
  description: string;
  specialPriceCents?: number;
  formattedSpecialPrice?: string;
  specialType: string;
  dayOfWeek?: number;
  validFrom?: string;
  validTo?: string;
  mealType: string;
  isActive: boolean;
  updatedAt: string;
  menuItem?: MenuItem;
}

export interface MenuStats {
  totalCategories: number;
  totalItems: number;
  totalSpecials: number;
  itemsByMealType: Array<{ mealType: string; count: number }>;
  itemsByDietaryType: Array<{ type: string; count: number }>;
  averagePrice: number;
  minPrice: number;
  maxPrice: number;
  currency: string;
}

export interface CreateMenuCategoryRequest {
  name: string;
  description?: string;
  mealType: string;
  displayOrder: number;
  isActive: boolean;
}

export interface UpdateMenuCategoryRequest {
  name: string;
  description?: string;
  mealType: string;
  displayOrder: number;
  isActive: boolean;
}

export interface CreateMenuItemRequest {
  menuCategoryId: number;
  name: string;
  description: string;
  priceCents: number;
  currency: string;
  allergens?: string;
  mealType: string;
  isVegetarian: boolean;
  isVegan: boolean;
  isGlutenFree: boolean;
  isSpicy: boolean;
  isAvailable: boolean;
  isSpecial: boolean;
  tags: string[];
}

export interface UpdateMenuItemRequest {
  menuCategoryId: number;
  name: string;
  description: string;
  priceCents: number;
  currency: string;
  allergens?: string;
  mealType: string;
  isVegetarian: boolean;
  isVegan: boolean;
  isGlutenFree: boolean;
  isSpicy: boolean;
  isAvailable: boolean;
  isSpecial: boolean;
  tags: string[];
}

export interface CreateMenuSpecialRequest {
  menuItemId?: number;
  title: string;
  description: string;
  specialPriceCents?: number;
  specialType: string;
  dayOfWeek?: number;
  validFrom?: string;
  validTo?: string;
  mealType: string;
  isActive: boolean;
}

export interface UpdateMenuSpecialRequest {
  menuItemId?: number;
  title: string;
  description: string;
  specialPriceCents?: number;
  specialType: string;
  dayOfWeek?: number;
  validFrom?: string;
  validTo?: string;
  mealType: string;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class MenuService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/menu`;

  // Category endpoints
  getCategories(mealType?: string): Observable<MenuCategory[]> {
    let params: any = {};
    if (mealType) {
      params.mealType = mealType;
    }
    return this.http.get<{categories: MenuCategory[]}>(`${this.apiUrl}/categories`, { params })
      .pipe(map(response => response.categories));
  }

  getCategory(id: number): Observable<MenuCategory> {
    return this.http.get<MenuCategory>(`${this.apiUrl}/categories/${id}`);
  }

  createCategory(category: CreateMenuCategoryRequest): Observable<MenuCategory> {
    return this.http.post<MenuCategory>(`${this.apiUrl}/categories`, category);
  }

  updateCategory(id: number, category: UpdateMenuCategoryRequest): Observable<MenuCategory> {
    return this.http.put<MenuCategory>(`${this.apiUrl}/categories/${id}`, category);
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/categories/${id}`);
  }

  // Item endpoints
  getItems(filters?: {
    categoryId?: number;
    mealType?: string;
    isVegetarian?: boolean;
    isVegan?: boolean;
    isGlutenFree?: boolean;
    isAvailable?: boolean;
    isSpecial?: boolean;
    search?: string;
  }): Observable<MenuItem[]> {
    return this.http.get<{items: MenuItem[]}>(`${this.apiUrl}/items`, { params: filters as any })
      .pipe(map(response => response.items));
  }

  getItem(id: number): Observable<MenuItem> {
    return this.http.get<MenuItem>(`${this.apiUrl}/items/${id}`);
  }

  createItem(item: CreateMenuItemRequest): Observable<MenuItem> {
    return this.http.post<MenuItem>(`${this.apiUrl}/items`, item);
  }

  updateItem(id: number, item: UpdateMenuItemRequest): Observable<MenuItem> {
    return this.http.put<MenuItem>(`${this.apiUrl}/items/${id}`, item);
  }

  deleteItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/items/${id}`);
  }

  // Special endpoints
  getSpecials(mealType?: string, active?: boolean): Observable<MenuSpecial[]> {
    const params: any = {};
    if (mealType) params.mealType = mealType;
    if (active !== undefined) params.active = active;
    return this.http.get<{specials: MenuSpecial[]}>(`${this.apiUrl}/specials`, { params })
      .pipe(map(response => response.specials));
  }

  createSpecial(special: CreateMenuSpecialRequest): Observable<MenuSpecial> {
    return this.http.post<MenuSpecial>(`${this.apiUrl}/specials`, special);
  }

  updateSpecial(id: number, special: UpdateMenuSpecialRequest): Observable<MenuSpecial> {
    return this.http.put<MenuSpecial>(`${this.apiUrl}/specials/${id}`, special);
  }

  deleteSpecial(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/specials/${id}`);
  }

  // Stats endpoint
  getStats(): Observable<MenuStats> {
    return this.http.get<MenuStats>(`${this.apiUrl}/stats`);
  }

  // Utility methods
  getMealTypes(): Array<{value: string, label: string}> {
    return [
      { value: 'all', label: 'All Day' },
      { value: 'breakfast', label: 'Breakfast' },
      { value: 'lunch', label: 'Lunch' },
      { value: 'dinner', label: 'Dinner' }
    ];
  }

  getSpecialTypes(): Array<{value: string, label: string}> {
    return [
      { value: 'daily', label: 'Daily Special' },
      { value: 'weekly', label: 'Weekly Special' },
      { value: 'seasonal', label: 'Seasonal' },
      { value: 'limited', label: 'Limited Time' }
    ];
  }

  getCurrencies(): Array<{value: string, label: string}> {
    return [
      { value: 'ZAR', label: 'South African Rand (ZAR)' },
      { value: 'USD', label: 'US Dollar (USD)' },
      { value: 'EUR', label: 'Euro (EUR)' },
      { value: 'GBP', label: 'British Pound (GBP)' }
    ];
  }

  getDaysOfWeek(): Array<{value: number, label: string}> {
    return [
      { value: 0, label: 'Sunday' },
      { value: 1, label: 'Monday' },
      { value: 2, label: 'Tuesday' },
      { value: 3, label: 'Wednesday' },
      { value: 4, label: 'Thursday' },
      { value: 5, label: 'Friday' },
      { value: 6, label: 'Saturday' }
    ];
  }

  formatPrice(priceCents: number, currency: string = 'ZAR'): string {
    const price = priceCents / 100;
    return `${currency} ${price.toFixed(2)}`;
  }

  parsePrice(formattedPrice: string): { priceCents: number; currency: string } {
    const match = formattedPrice.match(/([A-Z]{3})\s+([\d.]+)/);
    if (match) {
      return {
        currency: match[1],
        priceCents: Math.round(parseFloat(match[2]) * 100)
      };
    }
    return { priceCents: 0, currency: 'ZAR' };
  }
}