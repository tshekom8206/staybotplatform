import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, interval } from 'rxjs';
import { NgbModal, NgbNavModule, NgbTooltipModule, NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../core/feather-icon/feather-icon.directive';
import { LostAndFoundService } from './services/lost-and-found.service';
import {
  LostItem,
  FoundItem,
  LostAndFoundMatch,
  LostFoundStats,
  LostItemCategory,
  RegisterFoundItemRequest,
  LostFoundFilter
} from './models/lost-and-found.models';

@Component({
  selector: 'app-lost-and-found',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbNavModule,
    NgbTooltipModule,
    NgbDropdownModule,
    FeatherIconDirective
  ],
  templateUrl: './lost-and-found.component.html',
  styleUrl: './lost-and-found.component.scss'
})
export class LostAndFoundComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private lostAndFoundService = inject(LostAndFoundService);
  private modalService = inject(NgbModal);
  private formBuilder = inject(FormBuilder);

  @ViewChild('registerFoundItemModal') registerFoundItemModal!: TemplateRef<any>;
  @ViewChild('itemDetailsPanel') itemDetailsPanel!: TemplateRef<any>;
  @ViewChild('matchVerificationModal') matchVerificationModal!: TemplateRef<any>;

  // Data properties
  lostItems: LostItem[] = [];
  foundItems: FoundItem[] = [];
  matches: LostAndFoundMatch[] = [];
  stats: LostFoundStats = {
    openReports: 0,
    itemsInStorage: 0,
    pendingMatches: 0,
    urgentItems: 0,
    totalLostItems: 0,
    totalFoundItems: 0,
    totalMatched: 0,
    totalClaimed: 0,
    matchSuccessRate: 0
  };

  // UI State
  loading = true;
  error: string | null = null;
  activeTab: 'lost' | 'found' | 'matches' | 'claimed' = 'lost';
  selectedItem: LostItem | FoundItem | null = null;
  selectedMatch: LostAndFoundMatch | null = null;
  selectedCategory: LostItemCategory | null = null;

  // Filters
  searchTerm = '';
  categoryFilter: LostItemCategory | 'all' = 'all';
  urgencyFilter: 'all' | 'urgent' | 'high-value' | 'recent' = 'all';
  sortBy: 'newest' | 'urgent' | 'match-score' = 'newest';

  // Forms
  registerFoundItemForm!: FormGroup;
  matchVerificationForm!: FormGroup;

  // Categories for UI
  readonly categories: { value: LostItemCategory; label: string; icon: string }[] = [
    { value: 'Electronics', label: 'Electronics', icon: 'smartphone' },
    { value: 'Clothing', label: 'Clothing', icon: 'shopping-bag' },
    { value: 'Jewelry', label: 'Jewelry', icon: 'award' },
    { value: 'Documents', label: 'Documents', icon: 'file-text' },
    { value: 'Keys', label: 'Keys', icon: 'key' },
    { value: 'Personal', label: 'Personal Items', icon: 'briefcase' },
    { value: 'Other', label: 'Other', icon: 'package' }
  ];

  readonly locations = [
    'Pool Area',
    'Restaurant',
    'Lobby',
    'Gym',
    'Guest Room',
    'Conference Room',
    'Parking',
    'Other'
  ];

  ngOnInit(): void {
    this.initializeForms();
    this.loadData();
    this.setupAutoRefresh();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    this.registerFoundItemForm = this.formBuilder.group({
      itemName: ['', Validators.required],
      description: ['', Validators.required],
      category: ['', Validators.required],
      color: [''],
      brand: [''],
      foundLocation: ['', Validators.required],
      foundDate: [new Date().toISOString().slice(0, 16), Validators.required],
      foundBy: [''],
      storageLocation: ['', Validators.required],
      isHighValue: [false],
      notes: ['']
    });

    this.matchVerificationForm = this.formBuilder.group({
      verificationNotes: [''],
      rejectedReason: ['']
    });
  }

  private loadData(): void {
    this.loading = true;
    this.error = null;

    // Load stats
    this.lostAndFoundService.getStats()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.stats = stats;
        },
        error: (error) => {
          console.error('Error loading stats:', error);
        }
      });

    // Load lost items
    this.loadLostItems();

    // Load found items
    this.loadFoundItems();

    // Load matches
    this.loadMatches();
  }

  private loadLostItems(): void {
    const filter = this.buildFilter();
    this.lostAndFoundService.getLostItems(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (items) => {
          this.lostItems = items;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading lost items:', error);
          this.error = 'Failed to load lost items. Please try again.';
          this.loading = false;
        }
      });
  }

  private loadFoundItems(): void {
    const filter = this.buildFilter();
    this.lostAndFoundService.getFoundItems(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (items) => {
          this.foundItems = items;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading found items:', error);
          this.error = 'Failed to load found items. Please try again.';
          this.loading = false;
        }
      });
  }

  private loadMatches(): void {
    this.lostAndFoundService.getMatches('Pending')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (matches) => {
          this.matches = matches;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading matches:', error);
          this.error = 'Failed to load matches. Please try again.';
          this.loading = false;
        }
      });
  }

  private buildFilter(): LostFoundFilter {
    const filter: LostFoundFilter = {};

    if (this.searchTerm) filter.searchTerm = this.searchTerm;
    if (this.categoryFilter !== 'all') filter.category = this.categoryFilter;
    if (this.urgencyFilter !== 'all') filter.urgency = this.urgencyFilter;
    if (this.sortBy) filter.sortBy = this.sortBy;

    return filter;
  }

  private setupAutoRefresh(): void {
    // Refresh data every 30 seconds
    interval(30000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshData();
      });
  }

  // Public methods
  refreshData(): void {
    this.loadData();
  }

  applyFilters(): void {
    if (this.activeTab === 'lost') {
      this.loadLostItems();
    } else if (this.activeTab === 'found') {
      this.loadFoundItems();
    }
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.categoryFilter = 'all';
    this.urgencyFilter = 'all';
    this.sortBy = 'newest';
    this.applyFilters();
  }

  // Modal methods
  openRegisterFoundItemModal(): void {
    this.selectedCategory = null;
    this.registerFoundItemForm.reset({
      foundDate: new Date().toISOString().slice(0, 16),
      isHighValue: false
    });
    this.modalService.open(this.registerFoundItemModal, { size: 'lg', backdrop: 'static' });
  }

  selectCategory(category: LostItemCategory): void {
    this.selectedCategory = category;
    this.registerFoundItemForm.patchValue({ category });
  }

  registerFoundItem(): void {
    if (this.registerFoundItemForm.valid) {
      const formValue = this.registerFoundItemForm.value;
      const request: RegisterFoundItemRequest = {
        itemName: formValue.itemName,
        description: formValue.description,
        category: formValue.category,
        color: formValue.color || undefined,
        brand: formValue.brand || undefined,
        foundLocation: formValue.foundLocation,
        foundDate: new Date(formValue.foundDate),
        foundBy: formValue.foundBy,
        storageLocation: formValue.storageLocation,
        isHighValue: formValue.isHighValue || false,
        notes: formValue.notes || undefined
      };

      this.lostAndFoundService.registerFoundItem(request)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (item) => {
            this.modalService.dismissAll();
            this.refreshData();

            // Automatically find matches
            this.findMatchesForFoundItem(item.id);
          },
          error: (error) => {
            console.error('Error registering found item:', error);
            this.error = 'Failed to register found item. Please try again.';
          }
        });
    }
  }

  findMatchesForFoundItem(foundItemId: number): void {
    this.lostAndFoundService.findMatchesForFoundItem(foundItemId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (matches) => {
          this.matches = [...this.matches, ...matches];
          this.stats.pendingMatches += matches.length;
        },
        error: (error) => {
          console.error('Error finding matches:', error);
        }
      });
  }

  openMatchVerificationModal(match: LostAndFoundMatch): void {
    this.selectedMatch = match;
    this.matchVerificationForm.reset();
    this.modalService.open(this.matchVerificationModal, { size: 'lg', backdrop: 'static' });
  }

  confirmMatch(): void {
    if (this.selectedMatch) {
      const notes = this.matchVerificationForm.get('verificationNotes')?.value;

      this.lostAndFoundService.verifyMatch(this.selectedMatch.id, true, notes)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            this.refreshData();
          },
          error: (error) => {
            console.error('Error confirming match:', error);
            this.error = 'Failed to confirm match. Please try again.';
          }
        });
    }
  }

  rejectMatch(): void {
    if (this.selectedMatch) {
      const reason = this.matchVerificationForm.get('rejectedReason')?.value;

      this.lostAndFoundService.verifyMatch(this.selectedMatch.id, false, undefined, reason)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.modalService.dismissAll();
            this.refreshData();
          },
          error: (error) => {
            console.error('Error rejecting match:', error);
            this.error = 'Failed to reject match. Please try again.';
          }
        });
    }
  }

  viewLostItemDetails(item: LostItem): void {
    this.selectedItem = item;
    // Implement details panel logic
  }

  viewFoundItemDetails(item: FoundItem): void {
    this.selectedItem = item;
    // Implement details panel logic
  }

  // Utility methods
  getCategoryIcon(category: string): string {
    return this.lostAndFoundService.getCategoryIcon(category);
  }

  getStatusClass(status: string): string {
    return this.lostAndFoundService.getStatusClass(status);
  }

  isCheckoutToday(checkoutDate?: Date): boolean {
    return this.lostAndFoundService.isCheckoutToday(checkoutDate);
  }

  calculateDaysUntilDisposal(disposalDate: Date): number {
    return this.lostAndFoundService.calculateDaysUntilDisposal(disposalDate);
  }

  getMatchScoreColor(score: number): string {
    if (score >= 90) return 'success';
    if (score >= 75) return 'info';
    if (score >= 60) return 'warning';
    return 'secondary';
  }

  getTimeAgo(date: Date): string {
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 60) return `${diffMins} ${diffMins === 1 ? 'minute' : 'minutes'} ago`;
    if (diffHours < 24) return `${diffHours} ${diffHours === 1 ? 'hour' : 'hours'} ago`;
    return `${diffDays} ${diffDays === 1 ? 'day' : 'days'} ago`;
  }

  get filteredLostItems(): LostItem[] {
    return this.lostItems;
  }

  get filteredFoundItems(): FoundItem[] {
    return this.foundItems;
  }

  get filteredMatches(): LostAndFoundMatch[] {
    return this.matches;
  }

  get claimedItems(): (LostItem | FoundItem)[] {
    const claimedLost = this.lostItems.filter(item => item.status === 'Claimed');
    const claimedFound = this.foundItems.filter(item => item.status === 'Claimed');
    return [...claimedLost, ...claimedFound];
  }

  itemTrackBy(index: number, item: LostItem | FoundItem): number {
    return item.id;
  }

  matchTrackBy(index: number, match: LostAndFoundMatch): number {
    return match.id;
  }
}
