export type LostItemCategory = 'Electronics' | 'Clothing' | 'Jewelry' | 'Documents' | 'Keys' | 'Personal' | 'Other';
export type LostItemStatus = 'Open' | 'Matched' | 'Claimed' | 'Closed';
export type FoundItemStatus = 'InStorage' | 'Matched' | 'Claimed' | 'Disposed';
export type MatchStatus = 'Pending' | 'Verified' | 'Rejected';

export interface LostItem {
  id: number;
  referenceNumber: string;
  guestId: number;
  guestName: string;
  roomNumber: string;
  phoneNumber: string;
  itemName: string;
  description: string;
  category: LostItemCategory;
  color?: string;
  brand?: string;
  lastSeenLocation: string;
  lastSeenDate: Date;
  reportedDate: Date;
  status: LostItemStatus;
  isUrgent: boolean;
  checkoutDate?: Date;
  potentialMatches?: number;
  notes?: string;
  createdAt: Date;
  updatedAt?: Date;
}

export interface FoundItem {
  id: number;
  referenceNumber: string;
  itemName: string;
  description: string;
  category: LostItemCategory;
  color?: string;
  brand?: string;
  foundLocation: string;
  foundDate: Date;
  foundBy: string;
  storageLocation: string;
  status: FoundItemStatus;
  isSecure: boolean;
  disposalDate: Date;
  daysUntilDisposal: number;
  isHighValue: boolean;
  notes?: string;
  createdAt: Date;
  updatedAt?: Date;
}

export interface LostAndFoundMatch {
  id: number;
  matchReferenceNumber: string;
  lostItemId: number;
  foundItemId: number;
  lostItem: LostItem;
  foundItem: FoundItem;
  matchScore: number;
  matchReasons: string[];
  status: MatchStatus;
  verifiedBy?: string;
  verifiedDate?: Date;
  rejectedReason?: string;
  notes?: string;
  createdAt: Date;
}

export interface LostFoundStats {
  openReports: number;
  itemsInStorage: number;
  pendingMatches: number;
  urgentItems: number;
  totalLostItems: number;
  totalFoundItems: number;
  totalMatched: number;
  totalClaimed: number;
  matchSuccessRate: number;
}

export interface RegisterFoundItemRequest {
  itemName: string;
  description: string;
  category: LostItemCategory;
  color?: string;
  brand?: string;
  foundLocation: string;
  foundDate: Date;
  foundBy: string;
  storageLocation: string;
  isHighValue: boolean;
  notes?: string;
}

export interface VerifyMatchRequest {
  matchId: number;
  isConfirmed: boolean;
  verificationNotes?: string;
  rejectedReason?: string;
}

export interface CloseLostItemRequest {
  lostItemId: number;
  closeReason: 'Found' | 'Cancelled' | 'Other';
  notes?: string;
}

export interface LostFoundFilter {
  searchTerm?: string;
  category?: LostItemCategory;
  urgency?: 'urgent' | 'high-value' | 'recent';
  sortBy?: 'newest' | 'urgent' | 'match-score';
  status?: LostItemStatus | FoundItemStatus;
  dateFrom?: Date;
  dateTo?: Date;
}
