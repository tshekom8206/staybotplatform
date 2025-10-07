import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbModal, NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbAlertModule, NgbDatepickerModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { BookingService } from '../../../../core/services/booking.service';
import { Booking, BookingStatus, CreateBookingRequest, UpdateBookingRequest } from '../../../../core/models/guest.model';

@Component({
  selector: 'app-bookings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbPaginationModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbDatepickerModule,
    FeatherIconDirective
  ],
  templateUrl: './bookings.component.html',
  styleUrl: './bookings.component.scss'
})
export class BookingsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private bookingService = inject(BookingService);
  private modalService = inject(NgbModal);
  private fb = inject(FormBuilder);

  @ViewChild('bookingModal') bookingModal!: TemplateRef<any>;

  bookings: Booking[] = [];
  filteredBookings: Booking[] = [];
  loading = true;
  saving = false;
  error: string | null = null;
  success: string | null = null;

  // Pagination
  currentPage = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 0;

  // Filters
  searchTerm = '';
  statusFilter: BookingStatus | 'all' = 'all';
  sourceFilter = 'all';

  // Form
  bookingForm!: FormGroup;
  selectedBooking: Booking | null = null;
  isEditMode = false;
  availableRooms: string[] = [];

  // Options
  statusOptions: BookingStatus[] = ['Confirmed', 'CheckedIn', 'CheckedOut', 'Cancelled'];
  sourceOptions = ['Direct', 'Booking.com', 'Airbnb', 'Expedia', 'Phone', 'Walk-in'];

  ngOnInit(): void {
    this.initializeForm();
    this.loadBookings();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.bookingForm = this.fb.group({
      guestName: ['', [Validators.required, Validators.maxLength(100)]],
      phone: ['', [Validators.required, Validators.pattern(/^\+?\d{10,15}$/)]],
      email: ['', [Validators.email]],
      roomNumber: ['', [Validators.required, Validators.maxLength(10)]],
      checkinDate: ['', Validators.required],
      checkoutDate: ['', Validators.required],
      status: ['Confirmed', Validators.required],
      source: ['Direct', Validators.required],
      numberOfGuests: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
      specialRequests: [''],
      roomRate: [null, [Validators.min(0)]],
      isRepeatGuest: [false]
    });
  }

  loadBookings(): void {
    this.loading = true;
    this.error = null;

    const filter = {
      page: this.currentPage,
      pageSize: this.pageSize,
      status: this.statusFilter !== 'all' ? this.statusFilter : undefined,
      search: this.searchTerm || undefined,
      source: this.sourceFilter !== 'all' ? this.sourceFilter : undefined
    };

    this.bookingService.getBookings(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.bookings = response.data;
          this.filteredBookings = this.bookings;
          this.currentPage = response.pagination.currentPage;
          this.pageSize = response.pagination.pageSize;
          this.totalCount = response.pagination.totalCount;
          this.totalPages = response.pagination.totalPages;
          this.loading = false;
        },
        error: (err) => {
          this.error = 'Failed to load bookings: ' + (err.error?.message || err.message);
          this.loading = false;
        }
      });
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.loadBookings();
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.loadBookings();
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.loadBookings();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = 'all';
    this.sourceFilter = 'all';
    this.currentPage = 1;
    this.loadBookings();
  }

  openCreateModal(): void {
    this.isEditMode = false;
    this.selectedBooking = null;
    this.bookingForm.reset({
      status: 'Confirmed',
      source: 'Direct',
      numberOfGuests: 1,
      isRepeatGuest: false
    });
    this.modalService.open(this.bookingModal, { size: 'lg', backdrop: 'static' });
  }

  openEditModal(booking: Booking): void {
    this.isEditMode = true;
    this.selectedBooking = booking;
    this.bookingForm.patchValue({
      guestName: booking.guestName,
      phone: booking.phone,
      email: booking.email || '',
      roomNumber: booking.roomNumber,
      checkinDate: booking.checkinDate,
      checkoutDate: booking.checkoutDate,
      status: booking.status,
      source: booking.source,
      numberOfGuests: booking.numberOfGuests,
      specialRequests: booking.specialRequests || '',
      roomRate: booking.roomRate || null,
      isRepeatGuest: booking.isRepeatGuest
    });
    this.modalService.open(this.bookingModal, { size: 'lg', backdrop: 'static' });
  }

  closeModal(): void {
    this.modalService.dismissAll();
    this.bookingForm.reset();
    this.selectedBooking = null;
  }

  saveBooking(): void {
    if (this.bookingForm.invalid) {
      Object.keys(this.bookingForm.controls).forEach(key => {
        this.bookingForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.saving = true;
    this.error = null;

    const formValue = this.bookingForm.value;

    if (this.isEditMode && this.selectedBooking) {
      const updateRequest: UpdateBookingRequest = {
        guestName: formValue.guestName,
        phone: formValue.phone,
        email: formValue.email || undefined,
        roomNumber: formValue.roomNumber,
        checkinDate: formValue.checkinDate,
        checkoutDate: formValue.checkoutDate,
        status: formValue.status,
        source: formValue.source,
        numberOfGuests: formValue.numberOfGuests,
        specialRequests: formValue.specialRequests || undefined,
        roomRate: formValue.roomRate || undefined
      };

      this.bookingService.updateBooking(this.selectedBooking.id, updateRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.success = 'Booking updated successfully';
            this.closeModal();
            this.loadBookings();
            this.saving = false;
          },
          error: (err) => {
            this.error = 'Failed to update booking: ' + (err.error?.message || err.message);
            this.saving = false;
          }
        });
    } else {
      const createRequest: CreateBookingRequest = {
        guestName: formValue.guestName,
        phone: formValue.phone,
        email: formValue.email || undefined,
        roomNumber: formValue.roomNumber,
        checkinDate: formValue.checkinDate,
        checkoutDate: formValue.checkoutDate,
        source: formValue.source,
        numberOfGuests: formValue.numberOfGuests,
        specialRequests: formValue.specialRequests || undefined,
        roomRate: formValue.roomRate || undefined,
        isRepeatGuest: formValue.isRepeatGuest
      };

      this.bookingService.createBooking(createRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.success = 'Booking created successfully';
            this.closeModal();
            this.loadBookings();
            this.saving = false;
          },
          error: (err) => {
            this.error = 'Failed to create booking: ' + (err.error?.message || err.message);
            this.saving = false;
          }
        });
    }
  }

  cancelBooking(booking: Booking): void {
    if (!confirm(`Are you sure you want to cancel the booking for ${booking.guestName}?`)) {
      return;
    }

    this.bookingService.cancelBooking(booking.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.success = 'Booking cancelled successfully';
          this.loadBookings();
        },
        error: (err) => {
          this.error = 'Failed to cancel booking: ' + (err.error?.message || err.message);
        }
      });
  }

  getStatusBadgeClass(status: BookingStatus): string {
    switch (status) {
      case 'Confirmed': return 'badge bg-primary';
      case 'CheckedIn': return 'badge bg-success';
      case 'CheckedOut': return 'badge bg-secondary';
      case 'Cancelled': return 'badge bg-danger';
      default: return 'badge bg-light text-dark';
    }
  }

  dismissAlert(): void {
    this.error = null;
    this.success = null;
  }
}
