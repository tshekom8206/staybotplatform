import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, forkJoin } from 'rxjs';
import { map, catchError, switchMap, tap } from 'rxjs/operators';
import { isPlatformBrowser } from '@angular/common';

export interface WeatherData {
  current: {
    temp: number;
    icon: string;
    description: string;
    weatherCode: number;
  };
  forecast: {
    date: string;
    dayName: string;
    tempMax: number;
    icon: string;
  }[];
  location: string;
  lastUpdated: Date;
}

interface GeocodingResult {
  results?: {
    name: string;
    latitude: number;
    longitude: number;
    country: string;
  }[];
}

interface OpenMeteoResponse {
  current?: {
    temperature_2m: number;
    weather_code: number;
  };
  daily?: {
    time: string[];
    temperature_2m_max: number[];
    weather_code: number[];
  };
}

// Weather code to icon mapping (WMO Weather interpretation codes)
const weatherCodeToIcon: Record<number, { icon: string; description: string }> = {
  0: { icon: 'sun', description: 'Clear sky' },
  1: { icon: 'sun', description: 'Mainly clear' },
  2: { icon: 'cloud-sun', description: 'Partly cloudy' },
  3: { icon: 'cloud', description: 'Overcast' },
  45: { icon: 'cloud-fog', description: 'Foggy' },
  48: { icon: 'cloud-fog', description: 'Depositing rime fog' },
  51: { icon: 'cloud-drizzle', description: 'Light drizzle' },
  53: { icon: 'cloud-drizzle', description: 'Moderate drizzle' },
  55: { icon: 'cloud-drizzle', description: 'Dense drizzle' },
  56: { icon: 'cloud-sleet', description: 'Freezing drizzle' },
  57: { icon: 'cloud-sleet', description: 'Freezing drizzle' },
  61: { icon: 'cloud-rain', description: 'Slight rain' },
  63: { icon: 'cloud-rain', description: 'Moderate rain' },
  65: { icon: 'cloud-rain-heavy', description: 'Heavy rain' },
  66: { icon: 'cloud-sleet', description: 'Freezing rain' },
  67: { icon: 'cloud-sleet', description: 'Heavy freezing rain' },
  71: { icon: 'cloud-snow', description: 'Slight snow' },
  73: { icon: 'cloud-snow', description: 'Moderate snow' },
  75: { icon: 'cloud-snow', description: 'Heavy snow' },
  77: { icon: 'cloud-snow', description: 'Snow grains' },
  80: { icon: 'cloud-rain', description: 'Slight showers' },
  81: { icon: 'cloud-rain', description: 'Moderate showers' },
  82: { icon: 'cloud-rain-heavy', description: 'Violent showers' },
  85: { icon: 'cloud-snow', description: 'Slight snow showers' },
  86: { icon: 'cloud-snow', description: 'Heavy snow showers' },
  95: { icon: 'cloud-lightning-rain', description: 'Thunderstorm' },
  96: { icon: 'cloud-lightning-rain', description: 'Thunderstorm with hail' },
  99: { icon: 'cloud-lightning-rain', description: 'Severe thunderstorm' }
};

@Injectable({
  providedIn: 'root'
})
export class WeatherService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private readonly CACHE_KEY = 'weather_cache_v2'; // v2: uses city name from hotel info
  private readonly CACHE_DURATION = 3 * 60 * 60 * 1000; // 3 hours in ms
  private readonly GEOCODING_API = 'https://geocoding-api.open-meteo.com/v1/search';
  private readonly WEATHER_API = 'https://api.open-meteo.com/v1/forecast';

  /**
   * Get weather data for a city
   */
  getWeather(city: string): Observable<WeatherData | null> {
    if (!isPlatformBrowser(this.platformId)) {
      return of(null);
    }

    // Check cache first
    const cached = this.getCachedWeather(city);
    if (cached) {
      return of(cached);
    }

    // Geocode city to get coordinates, then fetch weather
    return this.geocodeCity(city).pipe(
      switchMap(coords => {
        if (!coords) {
          return of(null);
        }
        return this.fetchWeather(coords.lat, coords.lng, coords.name);
      }),
      tap(data => {
        if (data) {
          this.cacheWeather(city, data);
        }
      }),
      catchError(error => {
        console.error('Weather service error:', error);
        return of(null);
      })
    );
  }

  /**
   * Get weather data using coordinates directly (preferred method)
   */
  getWeatherByCoords(lat: number, lng: number, locationName: string): Observable<WeatherData | null> {
    if (!isPlatformBrowser(this.platformId)) {
      return of(null);
    }

    const cacheKey = `coords_${lat.toFixed(2)}_${lng.toFixed(2)}`;

    // Check cache first
    const cached = this.getCachedWeather(cacheKey);
    if (cached) {
      return of(cached);
    }

    return this.fetchWeather(lat, lng, locationName).pipe(
      tap(data => {
        if (data) {
          this.cacheWeather(cacheKey, data);
        }
      }),
      catchError(error => {
        console.error('Weather service error:', error);
        return of(null);
      })
    );
  }

  /**
   * Geocode city name to coordinates
   */
  private geocodeCity(city: string): Observable<{ lat: number; lng: number; name: string } | null> {
    // Clean up city name - extract just the city from address
    const cleanCity = this.extractCityFromAddress(city);

    return this.http.get<GeocodingResult>(`${this.GEOCODING_API}?name=${encodeURIComponent(cleanCity)}&count=1&language=en&format=json`).pipe(
      map(response => {
        if (response.results && response.results.length > 0) {
          const result = response.results[0];
          return {
            lat: result.latitude,
            lng: result.longitude,
            name: result.name
          };
        }
        return null;
      }),
      catchError(() => of(null))
    );
  }

  /**
   * Extract city name from full address
   */
  private extractCityFromAddress(address: string): string {
    if (!address) return 'Cape Town'; // Default fallback

    // Try to extract city from common address formats
    // e.g., "123 Main Street, Cape Town, South Africa" -> "Cape Town"
    const parts = address.split(',').map(p => p.trim());

    // Usually city is the second or third part
    if (parts.length >= 2) {
      // Skip street address, return likely city name
      return parts[parts.length - 2] || parts[1] || parts[0];
    }

    return address;
  }

  /**
   * Fetch weather from Open-Meteo API
   */
  private fetchWeather(lat: number, lng: number, locationName: string): Observable<WeatherData | null> {
    const url = `${this.WEATHER_API}?latitude=${lat}&longitude=${lng}&current=temperature_2m,weather_code&daily=temperature_2m_max,weather_code&timezone=auto&forecast_days=4`;

    return this.http.get<OpenMeteoResponse>(url).pipe(
      map(response => {
        if (!response.current || !response.daily) {
          return null;
        }

        const currentCode = response.current.weather_code;
        const currentWeather = weatherCodeToIcon[currentCode] || { icon: 'cloud', description: 'Unknown' };

        const forecast = response.daily.time.map((date, index) => {
          const code = response.daily!.weather_code[index];
          const weather = weatherCodeToIcon[code] || { icon: 'cloud', description: 'Unknown' };
          const dateObj = new Date(date);

          return {
            date,
            dayName: index === 0 ? 'Today' : dateObj.toLocaleDateString('en', { weekday: 'short' }),
            tempMax: Math.round(response.daily!.temperature_2m_max[index]),
            icon: weather.icon
          };
        });

        return {
          current: {
            temp: Math.round(response.current.temperature_2m),
            icon: currentWeather.icon,
            description: currentWeather.description,
            weatherCode: currentCode
          },
          forecast,
          location: locationName,
          lastUpdated: new Date()
        };
      }),
      catchError(() => of(null))
    );
  }

  /**
   * Get cached weather data
   */
  private getCachedWeather(city: string): WeatherData | null {
    if (!isPlatformBrowser(this.platformId)) return null;

    try {
      const cacheKey = `${this.CACHE_KEY}_${city.toLowerCase().replace(/\s+/g, '_')}`;
      const cached = localStorage.getItem(cacheKey);
      if (!cached) return null;

      const data = JSON.parse(cached);
      const cacheTime = new Date(data.lastUpdated).getTime();

      if (Date.now() - cacheTime < this.CACHE_DURATION) {
        data.lastUpdated = new Date(data.lastUpdated);
        return data;
      }

      // Cache expired
      localStorage.removeItem(cacheKey);
      return null;
    } catch {
      return null;
    }
  }

  /**
   * Cache weather data
   */
  private cacheWeather(city: string, data: WeatherData): void {
    if (!isPlatformBrowser(this.platformId)) return;

    try {
      const cacheKey = `${this.CACHE_KEY}_${city.toLowerCase().replace(/\s+/g, '_')}`;
      localStorage.setItem(cacheKey, JSON.stringify(data));
    } catch {
      // Ignore storage errors
    }
  }
}
