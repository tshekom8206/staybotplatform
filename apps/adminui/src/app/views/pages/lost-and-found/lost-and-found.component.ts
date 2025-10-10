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
  @ViewChild('editFoundItemModal') editFoundItemModal!: TemplateRef<any>;
  @ViewChild('itemDetailsModal') itemDetailsModal!: TemplateRef<any>;
  @ViewChild('lostItemDetailsModal') lostItemDetailsModal!: TemplateRef<any>;
  @ViewChild('matchVerificationModal') matchVerificationModal!: TemplateRef<any>;
  @ViewChild('contactGuestModal') contactGuestModal!: TemplateRef<any>;

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
  editFoundItemForm!: FormGroup;
  matchVerificationForm!: FormGroup;
  contactGuestForm!: FormGroup;
  selectedLostItem: LostItem | null = null;
  selectedFoundItem: FoundItem | null = null;

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

    this.contactGuestForm = this.formBuilder.group({
      message: ['', Validators.required],
      messageTemplate: ['custom']
    });

    this.editFoundItemForm = this.formBuilder.group({
      itemName: ['', Validators.required],
      description: ['', Validators.required],
      category: ['', Validators.required],
      color: [''],
      brand: [''],
      foundLocation: ['', Validators.required],
      storageLocation: ['', Validators.required],
      foundBy: [''],
      isHighValue: [false],
      notes: ['']
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
          if (matches && matches.length > 0) {
            this.matches = [...this.matches, ...matches];
            this.stats.pendingMatches += matches.length;

            // Switch to matches tab to show results
            this.activeTab = 'matches';

            // Show success message
            alert(`Found ${matches.length} potential ${matches.length === 1 ? 'match' : 'matches'}! Switching to Matches tab to review.`);
          } else {
            alert('No potential matches found for this item at this time.');
          }
        },
        error: (error) => {
          console.error('Error finding matches:', error);
          alert('Failed to search for matches. Please try again.');
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
    this.selectedLostItem = item;
    this.modalService.open(this.lostItemDetailsModal, { size: 'lg', centered: true });
  }

  viewMatchesForLostItem(item: LostItem, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }

    // Switch to matches tab and filter by this lost item
    this.activeTab = 'matches';

    // Load matches for this specific lost item
    this.lostAndFoundService.getMatchesForLostItem(item.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (matches) => {
          this.matches = matches;
        },
        error: (error) => {
          console.error('Error loading matches for lost item:', error);
          this.error = 'Failed to load matches. Please try again.';
        }
      });
  }

  contactGuest(item: LostItem, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }

    this.selectedLostItem = item;

    // Pre-fill message with item details
    const defaultMessage = `Hi ${item.guestName}, we have an update about your lost ${item.itemName}. Please contact the front desk or reply to this message for more information.`;

    this.contactGuestForm.patchValue({
      message: defaultMessage,
      messageTemplate: 'custom'
    });

    this.modalService.open(this.contactGuestModal, { size: 'lg', backdrop: 'static' });
  }

  sendMessageToGuest(): void {
    if (this.contactGuestForm.valid && this.selectedLostItem) {
      const message = this.contactGuestForm.get('message')?.value;

      // TODO: Integrate with actual messaging API
      console.log('Sending message to guest:', {
        guestName: this.selectedLostItem.guestName,
        phoneNumber: this.selectedLostItem.phoneNumber,
        roomNumber: this.selectedLostItem.roomNumber,
        message: message
      });

      // Close modal and show success
      this.modalService.dismissAll();
      alert(`Message sent to ${this.selectedLostItem.guestName} (${this.selectedLostItem.phoneNumber})`);
    }
  }

  useMessageTemplate(template: string): void {
    if (!this.selectedLostItem) return;

    let message = '';
    const itemName = this.selectedLostItem.itemName;
    const guestName = this.selectedLostItem.guestName;

    switch (template) {
      case 'found':
        message = `Hi ${guestName}, good news! We found an item matching your description of a ${itemName}. Please visit the front desk to verify and claim it.`;
        break;
      case 'need_info':
        message = `Hi ${guestName}, we need more information about the ${itemName} you reported as lost. Can you provide additional details to help us locate it?`;
        break;
      case 'checkout_reminder':
        message = `Hi ${guestName}, this is a reminder that you reported a lost ${itemName}. You're checking out today - please visit the front desk if you'd like to follow up.`;
        break;
      default:
        message = `Hi ${guestName}, we have an update about your lost ${itemName}. Please contact the front desk for more information.`;
    }

    this.contactGuestForm.patchValue({
      message: message,
      messageTemplate: template
    });
  }

  // Lost Item Actions
  editLostItem(item: LostItem, event?: Event): void {
    if (event) event.stopPropagation();
    console.log('Edit lost item:', item);
    alert(`Edit functionality for: ${item.itemName} - Coming soon!`);
  }

  markAsFound(item: LostItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (confirm(`Mark "${item.itemName}" as found?`)) {
      // Optimistic UI update
      const itemIndex = this.lostItems.findIndex(i => i.id === item.id);
      if (itemIndex > -1) {
        this.lostItems[itemIndex].status = 'Matched';
      }
      this.stats.openReports--;
      this.stats.totalMatched++;
      this.stats.itemsInStorage++;

      // Create a Found Item entry from the Lost Item
      const foundItemRequest: RegisterFoundItemRequest = {
        itemName: item.itemName,
        description: item.description,
        category: item.category,
        color: item.color,
        brand: item.brand,
        foundLocation: item.lastSeenLocation || 'Unknown',
        foundDate: new Date(),
        foundBy: 'Staff',
        storageLocation: 'Lost & Found Cabinet',
        isHighValue: false,
        notes: `Created from lost item report by ${item.guestName}`
      };

      // First, create the Found Item
      this.lostAndFoundService.registerFoundItem(foundItemRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (foundItem) => {
            // Then update the Lost Item status to 'Matched'
            this.lostAndFoundService.updateLostItem(item.id, { status: 'Matched' })
              .pipe(takeUntil(this.destroy$))
              .subscribe({
                next: () => {
                  alert('Item marked as found - guest will be notified!');
                  // Refresh to ensure data is in sync
                  this.refreshData();
                },
                error: (error) => {
                  console.error('Error updating lost item status:', error);
                  // Rollback optimistic update on error
                  if (itemIndex > -1) {
                    this.lostItems[itemIndex].status = item.status;
                  }
                  this.stats.openReports++;
                  this.stats.totalMatched--;
                  this.stats.itemsInStorage--;
                  alert('Failed to update lost item status. Please try again.');
                }
              });
          },
          error: (error) => {
            console.error('Error creating found item:', error);
            // Rollback optimistic update on error
            if (itemIndex > -1) {
              this.lostItems[itemIndex].status = item.status;
            }
            this.stats.openReports++;
            this.stats.totalMatched--;
            this.stats.itemsInStorage--;
            alert('Failed to mark item as found. Please try again.');
          }
        });
    }
  }

  viewItemDetails(item: LostItem, event?: Event): void {
    if (event) event.stopPropagation();
    this.selectedLostItem = item;
    this.modalService.open(this.lostItemDetailsModal, { size: 'lg', centered: true });
  }

  closeReport(item: LostItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (confirm(`Close the report for "${item.itemName}"? This will mark it as no longer active.`)) {
      console.log('Close report:', item);
      // TODO: Implement API call to close
      alert('Report closed successfully');
    }
  }

  deleteItem(item: LostItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (confirm(`Are you sure you want to delete the report for "${item.itemName}"? This action cannot be undone.`)) {
      console.log('Delete item:', item);
      // TODO: Implement API call to delete
      alert('Item deleted successfully');
    }
  }

  // Found Item Actions
  editFoundItem(item: FoundItem, event?: Event): void {
    if (event) event.stopPropagation();

    this.selectedFoundItem = item;
    this.selectedCategory = item.category as LostItemCategory;

    // Pre-populate the form with existing item data
    this.editFoundItemForm.patchValue({
      itemName: item.itemName,
      description: item.description,
      category: item.category,
      color: item.color || '',
      brand: item.brand || '',
      foundLocation: item.foundLocation,
      storageLocation: item.storageLocation,
      foundBy: item.foundBy,
      isHighValue: item.isHighValue,
      notes: item.notes || ''
    });

    this.modalService.open(this.editFoundItemModal, { size: 'lg', backdrop: 'static' });
  }

  saveFoundItemChanges(): void {
    if (this.editFoundItemForm.valid && this.selectedFoundItem) {
      const formValue = this.editFoundItemForm.value;

      // TODO: Call API to update the item
      console.log('Updating found item:', {
        id: this.selectedFoundItem.id,
        updates: formValue
      });

      this.modalService.dismissAll();
      alert(`Updated: ${formValue.itemName}`);

      // Refresh the data
      this.refreshData();
    }
  }

  updateStorageLocation(item: FoundItem, event?: Event): void {
    if (event) event.stopPropagation();
    const newLocation = prompt(`Update storage location for "${item.itemName}":`, item.storageLocation);
    if (newLocation) {
      console.log('Update storage location:', { item, newLocation });
      // TODO: Implement API call to update
      alert(`Storage location updated to: ${newLocation}`);
    }
  }

  markAsClaimed(item: FoundItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (confirm(`Mark "${item.itemName}" as claimed by guest?`)) {
      // Optimistic UI update
      const itemIndex = this.foundItems.findIndex(i => i.id === item.id);
      if (itemIndex > -1) {
        this.foundItems[itemIndex].status = 'Claimed';
      }
      this.stats.itemsInStorage--;
      this.stats.totalClaimed++;

      this.lostAndFoundService.updateFoundItem(item.id, { status: 'Claimed' })
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            alert('Item marked as claimed successfully!');
            // Refresh to ensure data is in sync
            this.refreshData();
          },
          error: (error) => {
            console.error('Error marking item as claimed:', error);
            // Rollback optimistic update on error
            if (itemIndex > -1) {
              this.foundItems[itemIndex].status = item.status;
            }
            this.stats.itemsInStorage++;
            this.stats.totalClaimed--;
            alert('Failed to mark item as claimed. Please try again.');
          }
        });
    }
  }

  viewFoundItemDetails(item: FoundItem, event?: Event): void {
    if (event) event.stopPropagation();
    this.selectedFoundItem = item;
    this.modalService.open(this.itemDetailsModal, { size: 'lg', centered: true });
  }

  markAsDisposed(item: FoundItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (confirm(`Mark "${item.itemName}" as disposed? This action should only be taken after the disposal period has expired.`)) {
      console.log('Mark as disposed:', item);
      // TODO: Implement API call to update status
      alert('Item marked as disposed');
    }
  }

  deleteFoundItem(item: FoundItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (confirm(`Are you sure you want to delete "${item.itemName}"? This action cannot be undone.`)) {
      console.log('Delete found item:', item);
      // TODO: Implement API call to delete
      alert('Item deleted successfully');
    }
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
    // Only show Lost Items with status "Open"
    return this.lostItems.filter(item => item.status === 'Open');
  }

  get filteredFoundItems(): FoundItem[] {
    // Only show Found Items with status "InStorage"
    return this.foundItems.filter(item => item.status === 'InStorage');
  }

  get filteredMatches(): LostAndFoundMatch[] {
    // Only show matches with status "Pending"
    return this.matches.filter(match => match.status === 'Pending');
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
