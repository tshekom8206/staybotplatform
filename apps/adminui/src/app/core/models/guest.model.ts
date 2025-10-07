export interface Guest {
  id: number;
  tenantId: number;
  phoneNumber: string;
  name?: string;
  email?: string;
  roomNumber?: string;
  bookingId?: number;
  checkInDate?: Date;
  checkOutDate?: Date;
  status: GuestStatus;
  lastMessageAt?: Date;
  createdAt: Date;
  booking?: Booking;
  conversations?: Conversation[];
}

export type GuestStatus = 'PreArrival' | 'CheckedIn' | 'CheckedOut' | 'Active';

export interface Booking {
  id: number;
  tenantId: number;
  guestName: string;
  phone: string;
  email?: string;
  roomNumber: string;
  checkinDate: string; // DateOnly as string
  checkoutDate: string; // DateOnly as string
  status: BookingStatus;
  numberOfGuests: number;
  specialRequests?: string;
  source: string;
  roomRate?: number;
  totalNights?: number;
  totalRevenue?: number;
  isRepeatGuest: boolean;
  createdAt: Date;
}

export type BookingStatus = 'Confirmed' | 'CheckedIn' | 'CheckedOut' | 'Cancelled' | 'NoShow';

export interface CreateBookingRequest {
  guestName: string;
  phone: string;
  email?: string;
  roomNumber: string;
  checkinDate: string;
  checkoutDate: string;
  source?: string;
  numberOfGuests?: number;
  specialRequests?: string;
  roomRate?: number;
  isRepeatGuest?: boolean;
}

export interface UpdateBookingRequest {
  guestName?: string;
  phone?: string;
  email?: string;
  roomNumber?: string;
  checkinDate?: string;
  checkoutDate?: string;
  status?: BookingStatus;
  source?: string;
  numberOfGuests?: number;
  specialRequests?: string;
  roomRate?: number;
}

export interface BookingStatistics {
  totalBookings: number;
  confirmedBookings: number;
  checkedInBookings: number;
  checkedOutBookings: number;
  cancelledBookings: number;
  todayCheckins: number;
  todayCheckouts: number;
  upcomingCheckins: number;
  totalRevenue: number;
  averageStayDuration: number;
  occupancyRate: number;
}

export interface BookingFilter {
  page?: number;
  pageSize?: number;
  status?: BookingStatus;
  search?: string;
  checkinFrom?: string;
  checkinTo?: string;
  checkoutFrom?: string;
  checkoutTo?: string;
  source?: string;
}

export interface Conversation {
  id: number;
  tenantId: number;
  waUserPhone: string;
  status: ConversationStatus;
  lastBotReplyAt?: Date;
  createdAt: Date;
  messages?: Message[];
}

export type ConversationStatus = 'Active' | 'Closed' | 'Pending';

export interface Message {
  id: number;
  conversationId: number;
  tenantId: number;
  body: string;
  direction: MessageDirection;
  messageType: MessageType;
  createdAt: Date;
  readAt?: Date;
  usedRag?: boolean;
  model?: string;
}

export type MessageDirection = 'Inbound' | 'Outbound';
export type MessageType = 'Text' | 'Image' | 'Document' | 'System';

export interface SendMessageRequest {
  phoneNumber: string;
  message: string;
  conversationId?: number;
}

export interface ConversationFilter {
  status?: ConversationStatus;
  dateFrom?: Date;
  dateTo?: Date;
  searchTerm?: string;
  roomNumber?: string;
}