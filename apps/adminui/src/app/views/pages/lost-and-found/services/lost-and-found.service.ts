import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';
import {
  LostItem,
  FoundItem,
  LostAndFoundMatch,
  LostFoundStats,
  RegisterFoundItemRequest,
  VerifyMatchRequest,
  CloseLostItemRequest,
  LostFoundFilter
} from '../models/lost-and-found.models';

@Injectable({
  providedIn: 'root'
})
export class LostAndFoundService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/lostandfound`;

  // GET /api/lostandfound/lost-items
  getLostItems(filter?: LostFoundFilter): Observable<LostItem[]> {
    let params = new HttpParams();

    if (filter?.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter?.category) params = params.set('category', filter.category);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.urgency) params = params.set('urgency', filter.urgency);
    if (filter?.sortBy) params = params.set('sortBy', filter.sortBy);
    if (filter?.dateFrom) params = params.set('dateFrom', filter.dateFrom.toISOString());
    if (filter?.dateTo) params = params.set('dateTo', filter.dateTo.toISOString());

    return this.http.get<LostItem[]>(`${this.apiUrl}/lost-items`, { params }).pipe(
      map(items => items.map(item => this.parseLostItemDates(item)))
    );
  }

  // GET /api/lostandfound/lost-items/{id}
  getLostItemById(id: number): Observable<LostItem> {
    return this.http.get<LostItem>(`${this.apiUrl}/lost-items/${id}`).pipe(
      map(item => this.parseLostItemDates(item))
    );
  }

  // GET /api/lostandfound/found-items
  getFoundItems(filter?: LostFoundFilter): Observable<FoundItem[]> {
    let params = new HttpParams();

    if (filter?.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter?.category) params = params.set('category', filter.category);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.sortBy) params = params.set('sortBy', filter.sortBy);

    return this.http.get<FoundItem[]>(`${this.apiUrl}/found-items`, { params }).pipe(
      map(items => items.map(item => this.parseFoundItemDates(item)))
    );
  }

  // GET /api/lostandfound/found-items/{id}
  getFoundItemById(id: number): Observable<FoundItem> {
    return this.http.get<FoundItem>(`${this.apiUrl}/found-items/${id}`).pipe(
      map(item => this.parseFoundItemDates(item))
    );
  }

  // GET /api/lostandfound/matches
  getMatches(status?: string): Observable<LostAndFoundMatch[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);

    return this.http.get<LostAndFoundMatch[]>(`${this.apiUrl}/matches`, { params }).pipe(
      map(matches => matches.map(match => this.parseMatchDates(match)))
    );
  }

  // GET /api/lostandfound/matches/{id}
  getMatchById(id: number): Observable<LostAndFoundMatch> {
    return this.http.get<LostAndFoundMatch>(`${this.apiUrl}/matches/${id}`).pipe(
      map(match => this.parseMatchDates(match))
    );
  }

  // POST /api/lostandfound/found-items
  registerFoundItem(request: RegisterFoundItemRequest): Observable<FoundItem> {
    return this.http.post<FoundItem>(`${this.apiUrl}/found-items`, request).pipe(
      map(item => this.parseFoundItemDates(item))
    );
  }

  // PUT /api/lostandfound/matches/{id}/verify
  verifyMatch(matchId: number, isConfirmed: boolean, notes?: string, rejectedReason?: string): Observable<LostAndFoundMatch> {
    const request: VerifyMatchRequest = {
      matchId,
      isConfirmed,
      verificationNotes: notes,
      rejectedReason
    };

    return this.http.put<LostAndFoundMatch>(`${this.apiUrl}/matches/${matchId}/verify`, request).pipe(
      map(match => this.parseMatchDates(match))
    );
  }

  // PUT /api/lostandfound/lost-items/{id}/close
  closeLostItem(request: CloseLostItemRequest): Observable<LostItem> {
    return this.http.put<LostItem>(`${this.apiUrl}/lost-items/${request.lostItemId}/close`, request).pipe(
      map(item => this.parseLostItemDates(item))
    );
  }

  // GET /api/lostandfound/stats
  getStats(): Observable<LostFoundStats> {
    const url = `${this.apiUrl}/stats`;
    console.log('[LostAndFoundService] Calling getStats URL:', url);
    console.log('[LostAndFoundService] Full apiUrl:', this.apiUrl);
    console.log('[LostAndFoundService] Environment apiUrl:', environment.apiUrl);
    return this.http.get<LostFoundStats>(url);
  }

  // PUT /api/lostandfound/found-items/{id}
  updateFoundItem(id: number, updates: Partial<FoundItem>): Observable<FoundItem> {
    return this.http.put<FoundItem>(`${this.apiUrl}/found-items/${id}`, updates).pipe(
      map(item => this.parseFoundItemDates(item))
    );
  }

  // PUT /api/lostandfound/lost-items/{id}
  updateLostItem(id: number, updates: Partial<LostItem>): Observable<LostItem> {
    return this.http.put<LostItem>(`${this.apiUrl}/lost-items/${id}`, updates).pipe(
      map(item => this.parseLostItemDates(item))
    );
  }

  // POST /api/lostandfound/found-items/{id}/find-matches
  findMatchesForFoundItem(foundItemId: number): Observable<LostAndFoundMatch[]> {
    return this.http.post<any>(`${this.apiUrl}/found-items/${foundItemId}/find-matches`, {}).pipe(
      map(response => {
        // Ensure response is an array
        const matchesArray = Array.isArray(response) ? response : [];
        return matchesArray.map(match => this.parseMatchDates(match));
      })
    );
  }

  // GET /api/lostandfound/lost-items/{id}/matches
  getMatchesForLostItem(lostItemId: number): Observable<LostAndFoundMatch[]> {
    return this.http.get<any>(`${this.apiUrl}/lost-items/${lostItemId}/matches`).pipe(
      map(response => {
        // Ensure response is an array
        const matchesArray = Array.isArray(response) ? response : [];
        return matchesArray.map(match => this.parseMatchDates(match));
      })
    );
  }

  // Helper methods to parse dates from API responses
  private parseLostItemDates(item: any): LostItem {
    return {
      ...item,
      // Map API property names to frontend model
      guestName: item.reporterName || item.guestName || 'Unknown',
      phoneNumber: item.reporterPhone || item.phoneNumber || '',
      lastSeenLocation: item.locationLost || item.lastSeenLocation || '',
      lastSeenDate: item.lastSeenDate ? new Date(item.lastSeenDate) : new Date(),
      reportedDate: item.reportedAt ? new Date(item.reportedAt) : (item.reportedDate ? new Date(item.reportedDate) : new Date()),
      checkoutDate: item.checkoutDate ? new Date(item.checkoutDate) : undefined,
      createdAt: item.createdAt ? new Date(item.createdAt) : new Date(),
      updatedAt: item.updatedAt ? new Date(item.updatedAt) : undefined
    };
  }

  private parseFoundItemDates(item: any): FoundItem {
    return {
      ...item,
      // Map API property names to frontend model
      foundBy: item.finderName || item.foundBy || 'Unknown',
      foundLocation: item.locationFound || item.foundLocation || '',
      foundDate: item.foundAt ? new Date(item.foundAt) : (item.foundDate ? new Date(item.foundDate) : new Date()),
      disposalDate: item.disposalDate ? new Date(item.disposalDate) : new Date(),
      createdAt: item.createdAt ? new Date(item.createdAt) : new Date(),
      updatedAt: item.updatedAt ? new Date(item.updatedAt) : undefined
    };
  }

  private parseMatchDates(match: any): LostAndFoundMatch {
    return {
      ...match,
      lostItem: match.lostItem ? this.parseLostItemDates(match.lostItem) : match.lostItem,
      foundItem: match.foundItem ? this.parseFoundItemDates(match.foundItem) : match.foundItem,
      verifiedDate: match.verifiedDate ? new Date(match.verifiedDate) : undefined,
      createdAt: match.createdAt ? new Date(match.createdAt) : new Date()
    };
  }

  // Utility methods
  getCategoryIcon(category: string): string {
    switch (category) {
      case 'Electronics': return 'smartphone';
      case 'Clothing': return 'shopping-bag';
      case 'Jewelry': return 'award';
      case 'Documents': return 'file-text';
      case 'Keys': return 'key';
      case 'Personal': return 'briefcase';
      case 'Other': return 'package';
      default: return 'box';
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Open': return 'badge bg-warning';
      case 'InStorage': return 'badge bg-info';
      case 'Matched': return 'badge bg-success';
      case 'Claimed': return 'badge bg-secondary';
      case 'Closed': return 'badge bg-dark';
      case 'Disposed': return 'badge bg-danger';
      case 'Pending': return 'badge bg-warning';
      case 'Verified': return 'badge bg-success';
      case 'Rejected': return 'badge bg-danger';
      default: return 'badge bg-light text-dark';
    }
  }

  calculateDaysUntilDisposal(disposalDate: Date): number {
    const now = new Date();
    const diffTime = disposalDate.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return Math.max(0, diffDays);
  }

  isCheckoutToday(checkoutDate?: Date): boolean {
    if (!checkoutDate) return false;
    const today = new Date();
    return checkoutDate.toDateString() === today.toDateString();
  }
}
