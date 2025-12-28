import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, MenuCategory, MenuItem } from '../../../core/services/guest-api.service';

@Component({
  selector: 'app-food-drinks',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header with Glassmorphism -->
        <div class="page-header">
          <a [routerLink]="selectedCategory() ? ['/food-drinks'] : ['/']" class="back-link" (click)="onBackClick($event)">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>

          @if (!selectedCategory()) {
            <h1 class="page-title">{{ 'foodDrinks.title' | translate }}</h1>
            <p class="page-subtitle">{{ 'foodDrinks.categories' | translate }}</p>
          } @else {
            <h1 class="page-title">{{ selectedCategory()!.name }}</h1>
            @if (selectedCategory()!.description) {
              <p class="page-subtitle">{{ selectedCategory()!.description }}</p>
            }
          }
        </div>

        @if (!selectedCategory()) {
          <!-- Categories View -->

          @if (loading()) {
            <div class="loading-spinner">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (categories().length === 0) {
            <div class="empty-state">
              <i class="bi bi-cup-hot"></i>
              <p>{{ 'foodDrinks.noMenu' | translate }}</p>
            </div>
          } @else {
            <div class="category-grid">
              @for (category of categories(); track category.id) {
                <div class="category-card" (click)="selectCategory(category)">
                  <div class="category-icon">
                    <i class="bi" [class]="category.icon"></i>
                  </div>
                  <div class="category-info">
                    <h3>{{ category.name }}</h3>
                    @if (category.description) {
                      <p>{{ category.description }}</p>
                    }
                    <span class="item-count">{{ category.items.length }} {{ 'foodDrinks.items' | translate }}</span>
                  </div>
                  <i class="bi bi-chevron-right"></i>
                </div>
              }
            </div>
          }
        } @else {
          <!-- Items View -->
          @if (selectedCategory()!.items.length === 0) {
            <div class="empty-state">
              <i class="bi bi-cup-hot"></i>
              <p>{{ 'foodDrinks.noItems' | translate }}</p>
            </div>
          } @else {
            <div class="menu-items">
              @for (item of selectedCategory()!.items; track item.id) {
                <div class="menu-item-card">
                  <!-- Food Image -->
                  <div class="item-image">
                    @if (item.imageUrl) {
                      <img [src]="item.imageUrl" [alt]="item.name" />
                    } @else {
                      <div class="image-placeholder">
                        <i class="bi" [class]="selectedCategory()!.icon"></i>
                      </div>
                    }
                  </div>

                  <!-- Item Content -->
                  <div class="item-content">
                    <div class="item-header">
                      <h4>{{ item.name }}</h4>
                      @if (item.isChefPick) {
                        <span class="chef-pick-badge">
                          <i class="bi bi-award"></i>
                        </span>
                      }
                    </div>

                    <!-- Tags Row -->
                    <div class="item-tags">
                      @if (item.isVegetarian) {
                        <span class="tag tag-vegetarian">
                          <i class="bi bi-check-circle-fill"></i> {{ 'foodDrinks.vegetarian' | translate }}
                        </span>
                      }
                      @if (item.hasVegetarianOption) {
                        <span class="tag tag-vegetarian-option">
                          <i class="bi bi-check-circle-fill"></i> {{ 'foodDrinks.vegetarianOption' | translate }}
                        </span>
                      }
                      @if (item.isVegan) {
                        <span class="tag tag-vegan">
                          <i class="bi bi-check-circle-fill"></i> {{ 'foodDrinks.vegan' | translate }}
                        </span>
                      }
                      @if (item.isGlutenFree) {
                        <span class="tag tag-gluten-free">
                          <i class="bi bi-circle"></i> {{ 'foodDrinks.glutenFree' | translate }}
                        </span>
                      }
                      @if (item.isPopular) {
                        <span class="tag tag-popular">{{ 'foodDrinks.popular' | translate }}</span>
                      }
                    </div>

                    <!-- Description -->
                    @if (item.description) {
                      <p class="item-description">{{ item.description }}</p>
                    }

                    <!-- Allergens -->
                    @if (item.allergens) {
                      <p class="allergens">
                        <i class="bi bi-exclamation-circle"></i> {{ item.allergens }}
                      </p>
                    }

                    <!-- Customize Button -->
                    @if (item.isCustomizable && item.customizeOptions) {
                      <div class="customize-row">
                        <span class="customize-options">{{ item.customizeOptions }}</span>
                        <button class="btn-customize">
                          Customize <i class="bi bi-gear-fill"></i>
                        </button>
                      </div>
                    }
                  </div>

                  <!-- Price & Add Button -->
                  <div class="item-actions">
                    <span class="item-price">{{ item.price }}</span>
                    <button class="btn-add" (click)="addItem(item)">
                      <i class="bi bi-plus"></i>
                    </button>
                  </div>
                </div>
              }
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 1rem 0;
    }

    /* Page Header - Clean, floating on background */
    .page-header {
      padding: 1.5rem 0 1.25rem;
      margin-bottom: 1rem;
    }

    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-decoration: none;
      font-size: 0.9rem;
      font-weight: 500;
      cursor: pointer;
      padding: 0.4rem 0.75rem;
      margin: -0.4rem -0.75rem 0.75rem;
      border-radius: 50px;
      transition: all 0.2s ease;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .back-link:hover {
      background: rgba(255, 255, 255, 0.15);
      color: white;
    }

    .back-link i {
      font-size: 1rem;
    }

    .page-title {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }

    .page-subtitle {
      font-size: 0.95rem;
      color: rgba(255, 255, 255, 0.9);
      margin: 0.25rem 0 0;
      text-shadow: 0 1px 6px rgba(0, 0, 0, 0.3);
    }

    .loading-spinner {
      display: flex;
      justify-content: center;
      padding: 3rem;
    }
    .empty-state {
      text-align: center;
      padding: 3rem;
      background: #f8f9fa;
      border-radius: 16px;
      color: #666;
    }
    .empty-state i {
      font-size: 3rem;
      margin-bottom: 1rem;
      opacity: 0.5;
    }

    /* Category Grid */
    .category-grid {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .category-card {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1rem;
      background: white;
      border-radius: 12px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
    }
    .category-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.12);
    }
    .category-icon {
      width: 50px;
      height: 50px;
      background: #1a1a1a;
      color: white;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
      flex-shrink: 0;
    }
    .category-info {
      flex: 1;
    }
    .category-info h3 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 600;
    }
    .category-info p {
      margin: 0.25rem 0 0;
      font-size: 0.85rem;
      color: #666;
    }
    .item-count {
      font-size: 0.8rem;
      color: #999;
    }
    .category-card > .bi-chevron-right {
      color: #1a1a1a;
    }

    /* Menu Items - New Design */
    .menu-items {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .menu-item-card {
      display: flex;
      align-items: flex-start;
      gap: 0.875rem;
      padding: 1rem;
      background: white;
      border-radius: 16px;
      box-shadow: 0 2px 12px rgba(0,0,0,0.08);
    }

    /* Food Image */
    .item-image {
      flex-shrink: 0;
      width: 56px;
      height: 56px;
      border-radius: 50%;
      overflow: hidden;
    }

    .item-image img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      border: 3px solid #f0f0f0;
      border-radius: 50%;
    }

    .image-placeholder {
      width: 100%;
      height: 100%;
      background: #1a1a1a;
      display: flex;
      align-items: center;
      justify-content: center;
      color: white;
      font-size: 1.3rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    /* Item Content */
    .item-content {
      flex: 1;
      min-width: 0;
    }

    .item-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.25rem;
    }

    .item-header h4 {
      margin: 0;
      font-size: 1rem;
      font-weight: 700;
      color: #1a1a1a;
    }

    .chef-pick-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 20px;
      height: 20px;
      background: #f0e6d3;
      color: #8b6914;
      border-radius: 50%;
      font-size: 0.65rem;
    }

    /* Tags */
    .item-tags {
      display: flex;
      flex-wrap: wrap;
      gap: 0.375rem;
      margin-bottom: 0.375rem;
    }

    .tag {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      font-size: 0.7rem;
      font-weight: 500;
      padding: 0.125rem 0.375rem;
      border-radius: 4px;
    }

    .tag i {
      font-size: 0.65rem;
    }

    .tag-vegetarian {
      color: #27ae60;
    }

    .tag-vegetarian i {
      color: #27ae60;
    }

    .tag-vegetarian-option {
      color: #27ae60;
    }

    .tag-vegan {
      color: #27ae60;
    }

    .tag-gluten-free {
      color: #666;
    }

    .tag-popular {
      background: #fff3cd;
      color: #856404;
      font-weight: 600;
    }

    /* Description */
    .item-description {
      margin: 0 0 0.25rem;
      font-size: 0.8rem;
      color: #666;
      line-height: 1.4;
    }

    /* Allergens */
    .allergens {
      margin: 0;
      font-size: 0.7rem;
      color: #999;
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .allergens i {
      color: #ccc;
    }

    /* Customize Row */
    .customize-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: 0.5rem;
      padding-top: 0.5rem;
      border-top: 1px dashed #eee;
    }

    .customize-options {
      font-size: 0.7rem;
      color: #888;
    }

    .btn-customize {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0.25rem 0.5rem;
      background: #27ae60;
      color: white;
      border: none;
      border-radius: 6px;
      font-size: 0.7rem;
      font-weight: 500;
      cursor: pointer;
    }

    .btn-customize:hover {
      background: #219a52;
    }

    /* Price & Actions */
    .item-actions {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 0.5rem;
      flex-shrink: 0;
    }

    .item-price {
      font-size: 0.95rem;
      font-weight: 700;
      color: #1a1a1a;
      white-space: nowrap;
    }

    .btn-add {
      width: 32px;
      height: 32px;
      background: #27ae60;
      color: white;
      border: none;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: all 0.2s ease;
      box-shadow: 0 2px 8px rgba(39, 174, 96, 0.3);
    }

    .btn-add:hover {
      background: #219a52;
      transform: scale(1.1);
    }

    .btn-add i {
      font-size: 1.1rem;
      font-weight: bold;
    }

    /* Responsive adjustments */
    @media (max-width: 360px) {
      .item-image {
        width: 48px;
        height: 48px;
      }

      .image-placeholder {
        font-size: 1.1rem;
      }

      .item-header h4 {
        font-size: 0.9rem;
      }

      .item-price {
        font-size: 0.85rem;
      }
    }
  `]
})
export class FoodDrinksComponent implements OnInit {
  private apiService = inject(GuestApiService);
  private route = inject(ActivatedRoute);

  categories = signal<MenuCategory[]>([]);
  selectedCategory = signal<MenuCategory | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loadMenu();
  }

  loadMenu(): void {
    this.loading.set(true);
    this.apiService.getMenu().subscribe({
      next: (response) => {
        this.categories.set(response.categories);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load menu:', error);
        this.loading.set(false);
      }
    });
  }

  selectCategory(category: MenuCategory): void {
    this.selectedCategory.set(category);
  }

  onBackClick(event: Event): void {
    if (this.selectedCategory()) {
      event.preventDefault();
      this.selectedCategory.set(null);
    }
  }

  addItem(item: MenuItem): void {
    // For now, just show a visual feedback
    // In future, this could add to cart or send order
    console.log('Added item:', item.name);
    // Could show a toast notification here
  }
}
