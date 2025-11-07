import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { GuestService } from '../../../../core/services/guest.service';

export interface InteractionDetail {
  id: number;
  date: Date;
  guestName: string;
  phoneNumber: string;
  roomNumber: string;
  interactionType: 'chat' | 'task' | 'emergency' | 'complaint' | 'request';
  staffMember: string;
  summary: string;
  status: 'resolved' | 'pending' | 'escalated';
  duration?: string;
  checkInDate?: Date;
  checkOutDate?: Date;
  bookingStatus?: string;
  conversationStatus?: string;
  messages: ConversationMessage[];
}

export interface ConversationMessage {
  id: number;
  body: string;
  direction: 'Inbound' | 'Outbound';
  createdAt: Date;
}

@Component({
  selector: 'app-interaction-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgbTooltipModule,
    FeatherIconDirective
  ],
  templateUrl: './interaction-detail.component.html',
  styleUrl: './interaction-detail.component.scss'
})
export class InteractionDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private guestService = inject(GuestService);

  interaction: InteractionDetail | null = null;
  loading = true;
  error: string | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadInteractionDetails(parseInt(id, 10));
    } else {
      this.error = 'Invalid interaction ID';
      this.loading = false;
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadInteractionDetails(id: number): void {
    this.loading = true;
    this.error = null;

    this.guestService.getGuestDetails(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (details: any) => {
          this.interaction = {
            id: details.id || id,
            date: new Date(),
            guestName: details.name || 'Unknown Guest',
            phoneNumber: details.phoneNumber || '',
            roomNumber: details.roomNumber || 'N/A',
            interactionType: this.determineInteractionType(details.messages || []),
            staffMember: 'AI Assistant',
            summary: this.generateSummary(details.messages || []),
            status: details.conversationStatus === 'Active' ? 'pending' : 'resolved',
            checkInDate: details.checkinDate ? new Date(details.checkinDate) : undefined,
            checkOutDate: details.checkoutDate ? new Date(details.checkoutDate) : undefined,
            bookingStatus: details.bookingStatus,
            conversationStatus: details.conversationStatus,
            messages: (details.messages || []).map((msg: any) => ({
              id: msg.id || 0,
              body: msg.body || '',
              direction: msg.direction || 'Inbound',
              createdAt: new Date(msg.createdAt || new Date())
            }))
          };
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading interaction details:', error);
          this.error = 'Failed to load interaction details. Please try again.';
          this.loading = false;
        }
      });
  }

  private determineInteractionType(messages: any[]): 'chat' | 'task' | 'emergency' | 'complaint' | 'request' {
    if (!messages || messages.length === 0) return 'chat';

    const lastMessage = messages[0]?.body?.toLowerCase() || '';

    if (lastMessage.includes('emergency') || lastMessage.includes('urgent') || lastMessage.includes('help'))
      return 'emergency';
    if (lastMessage.includes('complaint') || lastMessage.includes('problem') || lastMessage.includes('issue'))
      return 'complaint';
    if (lastMessage.includes('request') || lastMessage.includes('need') || lastMessage.includes('want'))
      return 'request';
    if (lastMessage.includes('task') || lastMessage.includes('maintenance') || lastMessage.includes('clean'))
      return 'task';

    return 'chat';
  }

  private generateSummary(messages: any[]): string {
    if (!messages || messages.length === 0) return 'No messages';

    const lastInboundMessage = messages.find(m => m.direction === 'Inbound');
    if (lastInboundMessage) {
      return lastInboundMessage.body.length > 100
        ? lastInboundMessage.body.substring(0, 100) + '...'
        : lastInboundMessage.body;
    }

    return messages[0].body.length > 100
      ? messages[0].body.substring(0, 100) + '...'
      : messages[0].body;
  }

  getTypeIcon(type: string): string {
    switch (type) {
      case 'chat': return 'message-square';
      case 'task': return 'clipboard';
      case 'emergency': return 'alert-triangle';
      case 'complaint': return 'frown';
      case 'request': return 'help-circle';
      default: return 'activity';
    }
  }

  getTypeClass(type: string): string {
    switch (type) {
      case 'chat': return 'badge bg-primary';
      case 'task': return 'badge bg-info';
      case 'emergency': return 'badge bg-danger';
      case 'complaint': return 'badge bg-warning text-dark';
      case 'request': return 'badge bg-success';
      default: return 'badge bg-secondary';
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'resolved': return 'badge bg-success';
      case 'pending': return 'badge bg-warning text-dark';
      case 'escalated': return 'badge bg-danger';
      default: return 'badge bg-secondary';
    }
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'resolved': return 'check-circle';
      case 'pending': return 'clock';
      case 'escalated': return 'alert-circle';
      default: return 'help-circle';
    }
  }

  getRelativeDate(date: Date | string): string {
    const now = new Date();
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    const diffInHours = Math.floor((now.getTime() - dateObj.getTime()) / (1000 * 60 * 60));

    if (diffInHours < 1) {
      const diffInMinutes = Math.floor((now.getTime() - dateObj.getTime()) / (1000 * 60));
      return diffInMinutes <= 1 ? 'Just now' : `${diffInMinutes} min ago`;
    } else if (diffInHours < 24) {
      return `${diffInHours}h ago`;
    } else {
      const diffInDays = Math.floor(diffInHours / 24);
      if (diffInDays === 1) return 'Yesterday';
      if (diffInDays < 7) return `${diffInDays} days ago`;
      return dateObj.toLocaleDateString('en-ZA', { timeZone: 'Africa/Johannesburg' });
    }
  }

  goBack(): void {
    this.router.navigate(['/guests/history']);
  }

  trackMessage(index: number, message: ConversationMessage): number {
    return message.id;
  }
}
