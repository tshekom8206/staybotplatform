import { Component, inject, signal, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { WeatherService, WeatherData } from '../../../core/services/weather.service';
import { GuestApiService } from '../../../core/services/guest-api.service';

@Component({
  selector: 'app-weather-widget',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    <div class="weather-widget" *ngIf="weather()">
      <div class="weather-current">
        <i class="bi bi-{{ weather()!.current.icon }}"></i>
        <span class="temp">{{ weather()!.current.temp }}°C</span>
        <span class="location">{{ weather()!.location }}</span>
      </div>
      <div class="weather-forecast">
        <div class="forecast-day" *ngFor="let day of weather()!.forecast">
          <span class="day-name">{{ day.dayName }}</span>
          <i class="bi bi-{{ day.icon }}"></i>
          <span class="day-temp">{{ day.tempMax }}°</span>
        </div>
      </div>
    </div>
    <div class="weather-loading" *ngIf="loading() && !weather()">
      <i class="bi bi-cloud-sun"></i>
      <span>Loading weather...</span>
    </div>
  `,
  styles: [`
    .weather-widget {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      background: rgba(255, 255, 255, 0.15);
      backdrop-filter: blur(10px);
      -webkit-backdrop-filter: blur(10px);
      border: 1px solid rgba(255, 255, 255, 0.25);
      border-radius: 16px;
      padding: 0.75rem 1rem;
      margin-top: 1rem;
      max-width: 360px;
      margin-left: auto;
      margin-right: auto;
    }

    .weather-current {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
    }

    .weather-current i {
      font-size: 1.5rem;
      filter: drop-shadow(0 1px 2px rgba(0, 0, 0, 0.2));
    }

    .weather-current .temp {
      font-size: 1.25rem;
      font-weight: 600;
      text-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
    }

    .weather-current .location {
      font-size: 0.8rem;
      opacity: 0.85;
      margin-left: 0.25rem;
    }

    .weather-forecast {
      display: flex;
      gap: 0.75rem;
    }

    .forecast-day {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.2rem;
      color: white;
    }

    .forecast-day .day-name {
      font-size: 0.65rem;
      text-transform: uppercase;
      opacity: 0.75;
      font-weight: 500;
    }

    .forecast-day i {
      font-size: 0.9rem;
      opacity: 0.9;
    }

    .forecast-day .day-temp {
      font-size: 0.75rem;
      font-weight: 600;
    }

    @media (max-width: 400px) {
      .weather-widget {
        flex-direction: column;
        gap: 0.75rem;
        padding: 0.75rem;
      }

      .weather-forecast {
        width: 100%;
        justify-content: space-around;
      }
    }

    .weather-loading {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      background: rgba(255, 255, 255, 0.15);
      backdrop-filter: blur(10px);
      -webkit-backdrop-filter: blur(10px);
      border: 1px solid rgba(255, 255, 255, 0.25);
      border-radius: 16px;
      padding: 0.75rem 1rem;
      margin-top: 1rem;
      max-width: 200px;
      margin-left: auto;
      margin-right: auto;
      color: white;
      font-size: 0.85rem;
    }

    .weather-loading i {
      font-size: 1.25rem;
      animation: pulse 1.5s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% { opacity: 0.6; }
      50% { opacity: 1; }
    }
  `]
})
export class WeatherWidgetComponent implements OnInit {
  private weatherService = inject(WeatherService);
  private guestApiService = inject(GuestApiService);

  weather = signal<WeatherData | null>(null);
  loading = signal(true);

  @Output() weatherLoaded = new EventEmitter<WeatherData>();

  ngOnInit(): void {
    this.loadWeather();
  }

  private loadWeather(): void {
    // Get hotel info to extract location coordinates
    this.guestApiService.getHotelInfo().subscribe({
      next: (hotelInfo) => {
        // Prefer using coordinates if available
        if (hotelInfo?.latitude && hotelInfo?.longitude) {
          // Use city field directly, fallback to hotel name
          const locationName = hotelInfo.city || hotelInfo.name || 'Local';
          this.fetchWeatherByCoords(hotelInfo.latitude, hotelInfo.longitude, locationName);
        } else if (hotelInfo?.address) {
          // Fallback to geocoding from address
          this.fetchWeatherForAddress(hotelInfo.address);
        } else {
          // Default fallback
          this.fetchWeatherForAddress('Cape Town, South Africa');
        }
      },
      error: () => {
        // Fallback to Cape Town if hotel info fails
        this.fetchWeatherForAddress('Cape Town, South Africa');
      }
    });
  }

  private fetchWeatherByCoords(lat: number, lng: number, locationName: string): void {
    this.weatherService.getWeatherByCoords(lat, lng, locationName).subscribe({
      next: (data) => {
        this.weather.set(data);
        this.loading.set(false);
        if (data) {
          this.weatherLoaded.emit(data);
        }
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  private fetchWeatherForAddress(address: string): void {
    this.weatherService.getWeather(address).subscribe({
      next: (data) => {
        this.weather.set(data);
        this.loading.set(false);
        if (data) {
          this.weatherLoaded.emit(data);
        }
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }
}
