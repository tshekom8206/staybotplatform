import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  MenuService,
  MenuCategory,
  MenuItem,
  MenuSpecial,
  MenuStats,
  CreateMenuCategoryRequest,
  UpdateMenuCategoryRequest,
  CreateMenuItemRequest,
  UpdateMenuItemRequest,
  CreateMenuSpecialRequest,
  UpdateMenuSpecialRequest
} from '../../../../core/services/menu.service';

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbModalModule,
    FeatherIconDirective
  ],
  templateUrl: './menu.component.html',
  styleUrl: './menu.component.scss'
})
export class MenuComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private menuService = inject(MenuService);
  private modalService = inject(NgbModal);

  @ViewChild('categoryModal') categoryModal!: TemplateRef<any>;
  @ViewChild('itemModal') itemModal!: TemplateRef<any>;
  @ViewChild('specialModal') specialModal!: TemplateRef<any>;

  // Data properties
  categories: MenuCategory[] = [];
  items: MenuItem[] = [];
  specials: MenuSpecial[] = [];
  stats: MenuStats | null = null;
  loading = false;
  saving = false;
  error: string | null = null;
  success: string | null = null;

  // View state
  viewMode: 'categories' | 'items' | 'specials' = 'categories';
  filteredData: any[] = [];

  // Modal properties
  selectedCategory: MenuCategory | null = null;
  selectedItem: MenuItem | null = null;
  selectedSpecial: MenuSpecial | null = null;
  isEditMode = false;
  categoryForm!: FormGroup;
  itemForm!: FormGroup;
  specialForm!: FormGroup;

  // Filter properties
  searchTerm = '';
  mealTypeFilter = 'all';
  categoryFilter = 'all';
  dietaryFilter = 'all';
  availabilityFilter = 'all';

  // Configuration data
  mealTypes: Array<{value: string, label: string}> = [];
  specialTypes: Array<{value: string, label: string}> = [];
  currencies: Array<{value: string, label: string}> = [];
  daysOfWeek: Array<{value: number, label: string}> = [];

  ngOnInit(): void {
    this.initializeForms();
    this.loadConfigurationData();
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    this.categoryForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      description: ['', [Validators.maxLength(500)]],
      mealType: ['all', Validators.required],
      displayOrder: [0, [Validators.required, Validators.min(0)]],
      isActive: [true]
    });

    this.itemForm = this.fb.group({
      menuCategoryId: ['', Validators.required],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', Validators.required],
      price: ['', [Validators.required, Validators.min(0)]],
      currency: ['ZAR', Validators.required],
      allergens: [''],
      mealType: ['all', Validators.required],
      isVegetarian: [false],
      isVegan: [false],
      isGlutenFree: [false],
      isSpicy: [false],
      isAvailable: [true],
      isSpecial: [false],
      tags: [[]]
    });

    this.specialForm = this.fb.group({
      menuItemId: [''],
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', Validators.required],
      specialPrice: [''],
      specialType: ['daily', Validators.required],
      dayOfWeek: [''],
      validFrom: [''],
      validTo: [''],
      mealType: ['all', Validators.required],
      isActive: [true]
    });
  }

  private loadConfigurationData(): void {
    this.mealTypes = this.menuService.getMealTypes();
    this.specialTypes = this.menuService.getSpecialTypes();
    this.currencies = this.menuService.getCurrencies();
    this.daysOfWeek = this.menuService.getDaysOfWeek();
  }

  loadData(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      categories: this.menuService.getCategories(),
      items: this.menuService.getItems(),
      specials: this.menuService.getSpecials(),
      stats: this.menuService.getStats()
    }).pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        this.categories = data.categories;
        this.items = data.items;
        this.specials = data.specials;
        this.stats = data.stats;
        this.applyFilters();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading menu data:', error);
        this.error = 'Failed to load menu data. Please try again.';
        this.loading = false;
      }
    });
  }

  setViewMode(mode: 'categories' | 'items' | 'specials'): void {
    this.viewMode = mode;
    this.applyFilters();
  }

  applyFilters(): void {
    let filtered: any[] = [];

    switch (this.viewMode) {
      case 'categories':
        filtered = [...(this.categories || [])];
        if (this.mealTypeFilter !== 'all') {
          filtered = filtered.filter(cat => cat.mealType === this.mealTypeFilter || cat.mealType === 'all');
        }
        break;

      case 'items':
        filtered = [...(this.items || [])];
        if (this.categoryFilter !== 'all') {
          filtered = filtered.filter(item => item.menuCategoryId.toString() === this.categoryFilter);
        }
        if (this.mealTypeFilter !== 'all') {
          filtered = filtered.filter(item => item.mealType === this.mealTypeFilter || item.mealType === 'all');
        }
        if (this.dietaryFilter !== 'all') {
          switch (this.dietaryFilter) {
            case 'vegetarian': filtered = filtered.filter(item => item.isVegetarian); break;
            case 'vegan': filtered = filtered.filter(item => item.isVegan); break;
            case 'glutenFree': filtered = filtered.filter(item => item.isGlutenFree); break;
            case 'spicy': filtered = filtered.filter(item => item.isSpicy); break;
          }
        }
        if (this.availabilityFilter !== 'all') {
          filtered = filtered.filter(item =>
            this.availabilityFilter === 'available' ? item.isAvailable : !item.isAvailable
          );
        }
        break;

      case 'specials':
        filtered = [...(this.specials || [])];
        if (this.mealTypeFilter !== 'all') {
          filtered = filtered.filter(special => special.mealType === this.mealTypeFilter || special.mealType === 'all');
        }
        break;
    }

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(item => {
        const searchableText = `${item.name || item.title} ${item.description || ''}`.toLowerCase();
        return searchableText.includes(term);
      });
    }

    this.filteredData = filtered;
  }

  // Category operations
  openCreateCategoryModal(): void {
    this.isEditMode = false;
    this.selectedCategory = null;
    this.categoryForm.reset({
      mealType: 'all',
      displayOrder: 0,
      isActive: true
    });
    this.modalService.open(this.categoryModal, { size: 'lg', backdrop: 'static' });
  }

  openEditCategoryModal(category: MenuCategory): void {
    this.isEditMode = true;
    this.selectedCategory = category;
    this.categoryForm.patchValue({
      name: category.name,
      description: category.description,
      mealType: category.mealType,
      displayOrder: category.displayOrder,
      isActive: category.isActive
    });
    this.modalService.open(this.categoryModal, { size: 'lg', backdrop: 'static' });
  }

  saveCategory(): void {
    if (this.categoryForm.invalid) {
      this.markFormGroupTouched(this.categoryForm);
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.categoryForm.value;
    const categoryData: CreateMenuCategoryRequest | UpdateMenuCategoryRequest = {
      name: formValue.name,
      description: formValue.description,
      mealType: formValue.mealType,
      displayOrder: formValue.displayOrder,
      isActive: formValue.isActive
    };

    const operation = this.isEditMode
      ? this.menuService.updateCategory(this.selectedCategory!.id, categoryData)
      : this.menuService.createCategory(categoryData);

    operation.pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (category) => {
          if (this.isEditMode) {
            const index = this.categories.findIndex(c => c.id === category.id);
            if (index !== -1) {
              this.categories[index] = category;
            }
          } else {
            this.categories.push(category);
          }
          this.applyFilters();
          this.saving = false;
          this.success = `Category ${this.isEditMode ? 'updated' : 'created'} successfully!`;
          this.modalService.dismissAll();
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving category:', error);
          this.error = this.getErrorMessage(error, 'category');
          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  deleteCategory(category: MenuCategory): void {
    if (!confirm(`Are you sure you want to delete the category "${category.name}"?`)) {
      return;
    }

    this.menuService.deleteCategory(category.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.categories = this.categories.filter(c => c.id !== category.id);
          this.applyFilters();
          this.success = 'Category deleted successfully!';
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error deleting category:', error);
          this.error = 'Failed to delete category. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  // Item operations
  openCreateItemModal(): void {
    this.isEditMode = false;
    this.selectedItem = null;
    this.itemForm.reset({
      currency: 'ZAR',
      mealType: 'all',
      isAvailable: true,
      tags: []
    });
    this.modalService.open(this.itemModal, { size: 'xl', backdrop: 'static' });
  }

  openEditItemModal(item: MenuItem): void {
    this.isEditMode = true;
    this.selectedItem = item;
    this.itemForm.patchValue({
      menuCategoryId: item.menuCategoryId,
      name: item.name,
      description: item.description,
      price: item.priceCents / 100,
      currency: item.currency,
      allergens: item.allergens,
      mealType: item.mealType,
      isVegetarian: item.isVegetarian,
      isVegan: item.isVegan,
      isGlutenFree: item.isGlutenFree,
      isSpicy: item.isSpicy,
      isAvailable: item.isAvailable,
      isSpecial: item.isSpecial,
      tags: item.tags || []
    });
    this.modalService.open(this.itemModal, { size: 'xl', backdrop: 'static' });
  }

  saveItem(): void {
    if (this.itemForm.invalid) {
      this.markFormGroupTouched(this.itemForm);
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.itemForm.value;
    const itemData: CreateMenuItemRequest | UpdateMenuItemRequest = {
      menuCategoryId: parseInt(formValue.menuCategoryId),
      name: formValue.name,
      description: formValue.description,
      priceCents: Math.round(formValue.price * 100),
      currency: formValue.currency,
      allergens: formValue.allergens,
      mealType: formValue.mealType,
      isVegetarian: formValue.isVegetarian,
      isVegan: formValue.isVegan,
      isGlutenFree: formValue.isGlutenFree,
      isSpicy: formValue.isSpicy,
      isAvailable: formValue.isAvailable,
      isSpecial: formValue.isSpecial,
      tags: formValue.tags || []
    };

    const operation = this.isEditMode
      ? this.menuService.updateItem(this.selectedItem!.id, itemData)
      : this.menuService.createItem(itemData);

    operation.pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (item) => {
          if (this.isEditMode) {
            const index = this.items.findIndex(i => i.id === item.id);
            if (index !== -1) {
              this.items[index] = item;
            }
          } else {
            this.items.push(item);
          }
          this.applyFilters();
          this.saving = false;
          this.success = `Menu item ${this.isEditMode ? 'updated' : 'created'} successfully!`;
          this.modalService.dismissAll();
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving item:', error);
          this.error = this.getErrorMessage(error, 'menu item');
          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  deleteItem(item: MenuItem): void {
    if (!confirm(`Are you sure you want to delete "${item.name}"?`)) {
      return;
    }

    this.menuService.deleteItem(item.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.items = this.items.filter(i => i.id !== item.id);
          this.applyFilters();
          this.success = 'Menu item deleted successfully!';
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error deleting item:', error);
          this.error = 'Failed to delete menu item. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  // Special operations
  openCreateSpecialModal(): void {
    this.isEditMode = false;
    this.selectedSpecial = null;
    this.specialForm.reset({
      specialType: 'daily',
      mealType: 'all',
      isActive: true
    });
    this.modalService.open(this.specialModal, { size: 'lg', backdrop: 'static' });
  }

  openEditSpecialModal(special: MenuSpecial): void {
    this.isEditMode = true;
    this.selectedSpecial = special;
    this.specialForm.patchValue({
      menuItemId: special.menuItemId,
      title: special.title,
      description: special.description,
      specialPrice: special.specialPriceCents ? special.specialPriceCents / 100 : '',
      specialType: special.specialType,
      dayOfWeek: special.dayOfWeek,
      validFrom: special.validFrom,
      validTo: special.validTo,
      mealType: special.mealType,
      isActive: special.isActive
    });
    this.modalService.open(this.specialModal, { size: 'lg', backdrop: 'static' });
  }

  saveSpecial(): void {
    if (this.specialForm.invalid) {
      this.markFormGroupTouched(this.specialForm);
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.specialForm.value;
    const specialData: CreateMenuSpecialRequest | UpdateMenuSpecialRequest = {
      menuItemId: formValue.menuItemId || undefined,
      title: formValue.title,
      description: formValue.description,
      specialPriceCents: formValue.specialPrice ? Math.round(formValue.specialPrice * 100) : undefined,
      specialType: formValue.specialType,
      dayOfWeek: formValue.dayOfWeek || undefined,
      validFrom: formValue.validFrom,
      validTo: formValue.validTo,
      mealType: formValue.mealType,
      isActive: formValue.isActive
    };

    const operation = this.isEditMode
      ? this.menuService.updateSpecial(this.selectedSpecial!.id, specialData)
      : this.menuService.createSpecial(specialData);

    operation.pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (special) => {
          if (this.isEditMode) {
            const index = this.specials.findIndex(s => s.id === special.id);
            if (index !== -1) {
              this.specials[index] = special;
            }
          } else {
            this.specials.push(special);
          }
          this.applyFilters();
          this.saving = false;
          this.success = `Special ${this.isEditMode ? 'updated' : 'created'} successfully!`;
          this.modalService.dismissAll();
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving special:', error);
          this.error = this.getErrorMessage(error, 'special');
          this.saving = false;
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  deleteSpecial(special: MenuSpecial): void {
    if (!confirm(`Are you sure you want to delete the special "${special.title}"?`)) {
      return;
    }

    this.menuService.deleteSpecial(special.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.specials = this.specials.filter(s => s.id !== special.id);
          this.applyFilters();
          this.success = 'Special deleted successfully!';
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error deleting special:', error);
          this.error = 'Failed to delete special. Please try again.';
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  // Tag management
  addTag(tag: string, formControl: any): void {
    const currentTags = formControl.value || [];
    if (tag && !currentTags.includes(tag)) {
      formControl.setValue([...currentTags, tag]);
    }
  }

  removeTag(tagToRemove: string, formControl: any): void {
    const currentTags = formControl.value || [];
    formControl.setValue(currentTags.filter((tag: string) => tag !== tagToRemove));
  }

  // Utility methods
  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(field => {
      const control = formGroup.get(field);
      control?.markAsTouched({ onlySelf: true });
    });
  }

  private getErrorMessage(error: any, itemType: string): string {
    if (error.status === 401) {
      return 'Your session has expired. Please log in again.';
    } else if (error.status === 403) {
      return `You do not have permission to manage ${itemType}s.`;
    } else if (error.status === 400) {
      return 'Invalid data provided. Please check your input and try again.';
    } else {
      return `Failed to save ${itemType}. Please try again.`;
    }
  }

  getCategoryName(categoryId: number): string {
    const category = this.categories.find(c => c.id === categoryId);
    return category ? category.name : 'Unknown Category';
  }

  getMealTypeLabel(mealType: string): string {
    const type = this.mealTypes.find(t => t.value === mealType);
    return type ? type.label : mealType;
  }

  getDietaryBadges(item: MenuItem): string[] {
    const badges: string[] = [];
    if (item.isVegetarian) badges.push('Vegetarian');
    if (item.isVegan) badges.push('Vegan');
    if (item.isGlutenFree) badges.push('Gluten-Free');
    if (item.isSpicy) badges.push('Spicy');
    return badges;
  }

  trackById(index: number, item: any): number {
    return item.id;
  }
}