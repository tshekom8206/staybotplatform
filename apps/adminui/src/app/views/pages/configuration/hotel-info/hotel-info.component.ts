import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { HotelInfoService, HotelInformation, UpdateHotelInfoRequest } from '../../../../core/services/hotel-info.service';
import { ServicesService, HotelService } from '../../../../core/services/services.service';


@Component({
  selector: 'app-hotel-info',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    FeatherIconDirective
  ],
  templateUrl: './hotel-info.component.html',
  styleUrl: './hotel-info.component.scss'
})
export class HotelInfoComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private hotelInfoService = inject(HotelInfoService);
  private servicesService = inject(ServicesService);

  hotelForm: FormGroup;
  addressForm: FormGroup;
  socialForm: FormGroup;
  policiesForm: FormGroup;
  settingsForm: FormGroup;
  wifiForm: FormGroup;
  roomConfigForm: FormGroup;

  loading = false;
  saving = false;
  error: string | null = null;
  success: string | null = null;

  // Current year for form validation
  currentYear = new Date().getFullYear();

  // Current hotel data
  currentHotelInfo: HotelInformation | null = null;

  // Available options (loaded from API)
  hotelCategories: Array<{ value: string, label: string }> = [];
  availableLanguages: Array<{ code: string, name: string }> = [];
  hotelServices: HotelService[] = [];
  timezones: string[] = [];
  currencies: Array<{ code: string, name: string }> = [];

  constructor() {
    this.initializeForms();
  }

  ngOnInit(): void {
    this.loadInitialData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    // Basic hotel information form
    this.hotelForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      description: ['', [Validators.required, Validators.maxLength(1000)]],
      category: ['', Validators.required],
      phone: ['', [Validators.required, Validators.pattern(/^\+?[\d\s\-\(\)]+$/)]],
      email: ['', [Validators.required, Validators.email]],
      website: ['', [Validators.pattern(/^https?:\/\/.+/)]],
      checkInTime: ['15:00', Validators.required],
      checkOutTime: ['11:00', Validators.required],
      numberOfRooms: [0, [Validators.required, Validators.min(1)]],
      numberOfFloors: [1, [Validators.required, Validators.min(1)]],
      establishedYear: [new Date().getFullYear(), [Validators.required, Validators.min(1800), Validators.max(new Date().getFullYear())]],
      defaultLanguage: ['en', Validators.required],
      supportedLanguages: [['en']]
    });

    // Address form
    this.addressForm = this.fb.group({
      street: ['', [Validators.required, Validators.maxLength(200)]],
      city: ['', [Validators.required, Validators.maxLength(100)]],
      state: ['', [Validators.required, Validators.maxLength(100)]],
      postalCode: ['', [Validators.required, Validators.maxLength(20)]],
      country: ['', [Validators.required, Validators.maxLength(100)]],
      latitude: [null, [Validators.min(-90), Validators.max(90)]],
      longitude: [null, [Validators.min(-180), Validators.max(180)]]
    });

    // Social media form
    this.socialForm = this.fb.group({
      facebook: ['', [Validators.pattern(/^https?:\/\/(www\.)?facebook\.com\/.+/)]],
      twitter: ['', [Validators.pattern(/^https?:\/\/(www\.)?twitter\.com\/.+/)]],
      instagram: ['', [Validators.pattern(/^https?:\/\/(www\.)?instagram\.com\/.+/)]],
      linkedin: ['', [Validators.pattern(/^https?:\/\/(www\.)?linkedin\.com\/.+/)]]
    });

    // Policies form
    this.policiesForm = this.fb.group({
      cancellationPolicy: ['', [Validators.required, Validators.maxLength(500)]],
      petPolicy: ['', [Validators.required, Validators.maxLength(500)]],
      smokingPolicy: ['', [Validators.required, Validators.maxLength(500)]],
      childPolicy: ['', [Validators.required, Validators.maxLength(500)]]
    });

    // Settings form
    this.settingsForm = this.fb.group({
      allowOnlineBooking: [true],
      requirePhoneVerification: [true],
      enableNotifications: [true],
      enableChatbot: [true],
      timezone: ['Africa/Johannesburg', Validators.required],
      currency: ['ZAR', Validators.required]
    });

    // WiFi credentials form
    this.wifiForm = this.fb.group({
      wifiNetwork: ['', [Validators.maxLength(100)]],
      wifiPassword: ['', [Validators.maxLength(100)]]
    });

    // Room configuration form
    this.roomConfigForm = this.fb.group({
      validRooms: ['', [Validators.maxLength(5000)]]
    });
  }

  private loadInitialData(): void {
    this.loading = true;
    this.error = null;

    // Load all required data in parallel
    forkJoin({
      hotelInfo: this.hotelInfoService.getHotelInfo(),
      categories: this.hotelInfoService.getHotelCategories(),
      languages: this.hotelInfoService.getAvailableLanguages(),
      services: this.servicesService.getServices(),
      timezones: this.hotelInfoService.getTimezones(),
      currencies: this.hotelInfoService.getCurrencies()
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.currentHotelInfo = data.hotelInfo;
          this.hotelCategories = data.categories;
          this.availableLanguages = data.languages;
          this.hotelServices = data.services;
          this.timezones = data.timezones;
          this.currencies = data.currencies;

          this.populateForms();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading hotel information:', error);
          this.error = 'Failed to load hotel information. Please try again.';
          this.loading = false;

          // Load fallback data if API calls fail
          this.loadFallbackData();
        }
      });
  }

  private loadFallbackData(): void {
    // Fallback data in case API calls fail
    this.hotelCategories = [
      { value: 'luxury', label: '5-Star Luxury' },
      { value: 'premium', label: '4-Star Premium' },
      { value: 'comfort', label: '3-Star Comfort' },
      { value: 'boutique', label: 'Boutique Hotel' },
      { value: 'business', label: 'Business Hotel' },
      { value: 'resort', label: 'Resort' }
    ];

    this.availableLanguages = [
      { code: 'en', name: 'English' },
      { code: 'es', name: 'Spanish' },
      { code: 'fr', name: 'French' },
      { code: 'de', name: 'German' },
      { code: 'it', name: 'Italian' },
      { code: 'pt', name: 'Portuguese' }
    ];

    this.hotelServices = []; // Services will be loaded from API or left empty

    this.timezones = [
      'Africa/Johannesburg',
      'UTC',
      'America/New_York',
      'America/Chicago',
      'America/Denver',
      'America/Los_Angeles',
      'Europe/London',
      'Europe/Paris',
      'Africa/Cairo',
      'Africa/Lagos',
      'Africa/Nairobi'
    ];

    this.currencies = [
      { code: 'USD', name: 'US Dollar' },
      { code: 'EUR', name: 'Euro' },
      { code: 'GBP', name: 'British Pound' },
      { code: 'ZAR', name: 'South African Rand' }
    ];
  }

  private populateForms(): void {
    if (!this.currentHotelInfo) return;

    const info = this.currentHotelInfo;

    // Populate basic information
    this.hotelForm.patchValue({
      name: info.name,
      description: info.description,
      category: info.category,
      phone: info.phone,
      email: info.email,
      website: info.website,
      checkInTime: info.checkInTime,
      checkOutTime: info.checkOutTime,
      numberOfRooms: info.numberOfRooms,
      numberOfFloors: info.numberOfFloors,
      establishedYear: info.establishedYear,
      defaultLanguage: info.defaultLanguage,
      supportedLanguages: info.supportedLanguages
    });

    // Populate address
    this.addressForm.patchValue(info.address);

    // Populate social media
    this.socialForm.patchValue(info.socialMedia);

    // Populate policies
    this.policiesForm.patchValue(info.policies);

    // Populate settings
    this.settingsForm.patchValue(info.settings);

    // Populate WiFi credentials
    if (info.wifi) {
      this.wifiForm.patchValue({
        wifiNetwork: info.wifi.network || '',
        wifiPassword: info.wifi.password || ''
      });
    }

    // Populate room configuration (from tenant or hotel info)
    if (info.validRooms) {
      this.roomConfigForm.patchValue({
        validRooms: info.validRooms
      });
    }
  }

  /**
   * Get count of configured rooms from the validRooms field
   */
  getRoomCount(): number {
    const validRooms = this.roomConfigForm.get('validRooms')?.value || '';
    if (!validRooms.trim()) return 0;

    return validRooms
      .split(',')
      .map((r: string) => r.trim())
      .filter((r: string) => r.length > 0)
      .length;
  }

  onLanguageToggle(languageCode: string): void {
    const currentLanguages = this.hotelForm.get('supportedLanguages')?.value || [];
    const index = currentLanguages.indexOf(languageCode);

    if (index > -1) {
      // Remove language if it exists (but ensure at least one remains)
      if (currentLanguages.length > 1) {
        currentLanguages.splice(index, 1);
      }
    } else {
      // Add language if it doesn't exist
      currentLanguages.push(languageCode);
    }

    this.hotelForm.patchValue({ supportedLanguages: currentLanguages });
  }

  isLanguageSelected(languageCode: string): boolean {
    const supportedLanguages = this.hotelForm.get('supportedLanguages')?.value || [];
    return supportedLanguages.includes(languageCode);
  }

  onServiceToggle(service: HotelService): void {
    // Toggle the service availability directly using the services API
    const newAvailabilityState = !service.isAvailable;

    // Create a complete UpdateServiceRequest with all required fields
    const updateRequest = {
      name: service.name,
      description: service.description,
      category: service.category,
      icon: service.icon,
      isAvailable: newAvailabilityState,
      isChargeable: service.isChargeable,
      price: service.price,
      currency: service.currency,
      pricingUnit: service.pricingUnit,
      availableHours: service.availableHours,
      contactMethod: service.contactMethod,
      contactInfo: service.contactInfo,
      priority: service.priority,
      specialInstructions: service.specialInstructions,
      imageUrl: service.imageUrl,
      requiresAdvanceBooking: service.requiresAdvanceBooking,
      advanceBookingHours: service.advanceBookingHours
    };

    this.servicesService.updateService(service.id, updateRequest)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (updatedService) => {
          // Update the local service object
          const index = this.hotelServices.findIndex(s => s.id === service.id);
          if (index !== -1) {
            this.hotelServices[index] = updatedService;
          }

          this.success = `${service.name} ${newAvailabilityState ? 'enabled' : 'disabled'} successfully!`;
          setTimeout(() => this.success = null, 3000);
        },
        error: (error) => {
          console.error('Error updating service:', error);
          this.error = `Failed to update ${service.name}. Please try again.`;
          setTimeout(() => this.error = null, 5000);
        }
      });
  }

  isServiceAvailable(service: HotelService): boolean {
    return service.isAvailable;
  }

  saveAllForms(): void {
    // Check if all forms are valid
    const formsValid = [
      this.hotelForm.valid,
      this.addressForm.valid,
      this.socialForm.valid,
      this.policiesForm.valid,
      this.settingsForm.valid,
      this.wifiForm.valid,
      this.roomConfigForm.valid
    ].every(valid => valid);

    if (!formsValid) {
      this.markAllFormsTouched();
      this.error = 'Please correct the errors in the form before saving.';
      setTimeout(() => this.error = null, 5000);
      return;
    }

    this.saving = true;
    this.error = null;

    // Compile all form data (features now managed separately through Services table)
    const updatedHotelInfo: UpdateHotelInfoRequest = {
      ...this.hotelForm.value,
      address: this.addressForm.value,
      socialMedia: this.socialForm.value,
      policies: this.policiesForm.value,
      settings: this.settingsForm.value,
      wifi: {
        network: this.wifiForm.get('wifiNetwork')?.value || '',
        password: this.wifiForm.get('wifiPassword')?.value || ''
      },
      validRooms: this.roomConfigForm.get('validRooms')?.value || ''
    };

    // Call API to update hotel information
    this.hotelInfoService.updateHotelInfo(updatedHotelInfo)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.currentHotelInfo = response;
          this.saving = false;
          this.success = 'Hotel information saved successfully!';

          // Clear success message after 5 seconds
          setTimeout(() => this.success = null, 5000);
        },
        error: (error) => {
          console.error('Error saving hotel information:', error);

          // Handle specific error cases
          if (error.status === 401) {
            this.error = 'Your session has expired. Please log in again to save changes.';
          } else if (error.status === 403) {
            this.error = 'You do not have permission to update hotel information.';
          } else if (error.status === 400) {
            this.error = 'Invalid data provided. Please check your input and try again.';
          } else if (error.status === 0) {
            this.error = 'Unable to connect to the server. Please check your internet connection.';
          } else {
            this.error = 'Failed to save hotel information. Please try again.';
          }

          this.saving = false;

          // Clear error message after 10 seconds for better visibility
          setTimeout(() => this.error = null, 10000);
        }
      });
  }

  private markAllFormsTouched(): void {
    [this.hotelForm, this.addressForm, this.socialForm, this.policiesForm, this.settingsForm, this.wifiForm, this.roomConfigForm].forEach(form => {
      Object.keys(form.controls).forEach(field => {
        const control = form.get(field);
        control?.markAsTouched({ onlySelf: true });
      });
    });
  }

  resetForms(): void {
    if (confirm('Are you sure you want to reset all forms? This will discard any unsaved changes.')) {
      this.populateForms();
      this.success = 'Forms have been reset to saved values.';
      setTimeout(() => this.success = null, 3000);
    }
  }

  getLanguageName(code: string): string {
    const language = this.availableLanguages.find(lang => lang.code === code);
    return language ? language.name : code;
  }

  getCurrencyName(code: string): string {
    const currency = this.currencies.find(curr => curr.code === code);
    return currency ? currency.name : code;
  }

  get allFormsValid(): boolean {
    return [
      this.hotelForm.valid,
      this.addressForm.valid,
      this.socialForm.valid,
      this.policiesForm.valid,
      this.settingsForm.valid,
      this.wifiForm.valid,
      this.roomConfigForm.valid
    ].every(valid => valid);
  }
}
