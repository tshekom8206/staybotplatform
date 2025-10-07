import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface SoundSettings {
  enabled: boolean;
  volume: number;
  taskCreationSound: boolean;
  taskCompletionSound: boolean;
  emergencySound: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class SoundService {
  private readonly STORAGE_KEY = 'hostr_sound_settings';
  private readonly SERVICE_BELL_PATH = '/sounds/servicebell.mp3';

  private defaultSettings: SoundSettings = {
    enabled: false, // Default to disabled for better UX
    volume: 0.7,
    taskCreationSound: true,
    taskCompletionSound: false,
    emergencySound: true
  };

  private settingsSubject = new BehaviorSubject<SoundSettings>(this.defaultSettings);
  public settings$ = this.settingsSubject.asObservable();

  private audioContext: AudioContext | null = null;
  private serviceBellBuffer: AudioBuffer | null = null;
  private isInitialized = false;

  constructor() {
    this.loadSettings();
    this.initializeAudio();
  }

  /**
   * Initialize audio context and preload sounds
   */
  private async initializeAudio(): Promise<void> {
    try {
      // Create audio context
      this.audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();

      // Preload service bell sound
      await this.preloadSound(this.SERVICE_BELL_PATH);

      this.isInitialized = true;
      console.log('SoundService initialized successfully');
    } catch (error) {
      console.warn('Failed to initialize audio:', error);
      this.isInitialized = false;
    }
  }

  /**
   * Preload a sound file into an AudioBuffer
   */
  private async preloadSound(path: string): Promise<void> {
    if (!this.audioContext) return;

    try {
      const response = await fetch(path);
      const arrayBuffer = await response.arrayBuffer();
      this.serviceBellBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
    } catch (error) {
      console.warn(`Failed to preload sound: ${path}`, error);
    }
  }

  /**
   * Resume audio context if suspended (required by browser autoplay policies)
   */
  private async resumeAudioContext(): Promise<void> {
    if (this.audioContext?.state === 'suspended') {
      try {
        await this.audioContext.resume();
      } catch (error) {
        console.warn('Failed to resume audio context:', error);
      }
    }
  }

  /**
   * Play service bell sound for task notifications
   */
  async playServiceBell(): Promise<void> {
    const settings = this.settingsSubject.value;
    console.log('🔊 SoundService.playServiceBell() called with settings:', settings);

    if (!settings.enabled) {
      console.log('🔇 Sound is disabled - skipping playback');
      return;
    }

    if (!settings.taskCreationSound) {
      console.log('🔇 Task creation sound is disabled - skipping playback');
      return;
    }

    if (!this.isInitialized) {
      console.log('❌ Sound service not initialized - skipping playback');
      return;
    }

    try {
      await this.resumeAudioContext();

      if (this.audioContext && this.serviceBellBuffer) {
        console.log('🎵 Playing service bell with volume:', settings.volume);
        const source = this.audioContext.createBufferSource();
        const gainNode = this.audioContext.createGain();

        source.buffer = this.serviceBellBuffer;
        gainNode.gain.value = settings.volume;

        source.connect(gainNode);
        gainNode.connect(this.audioContext.destination);

        source.start();
        console.log('✅ Service bell playback started successfully');
      } else {
        console.log('❌ Missing audio context or service bell buffer');
      }
    } catch (error) {
      console.error('❌ Failed to play service bell:', error);
      throw error;
    }
  }

  /**
   * Play emergency alert sound (could be different from service bell)
   */
  async playEmergencyAlert(): Promise<void> {
    const settings = this.settingsSubject.value;

    if (!settings.enabled || !settings.emergencySound) {
      return;
    }

    // For now, use service bell. Could be enhanced with different sound
    await this.playServiceBell();
  }

  /**
   * Test sound playback
   */
  async testSound(): Promise<void> {
    // Temporarily enable sound for testing
    const originalSettings = this.settingsSubject.value;
    this.settingsSubject.next({
      ...originalSettings,
      enabled: true,
      taskCreationSound: true
    });

    await this.playServiceBell();

    // Restore original settings
    this.settingsSubject.next(originalSettings);
  }

  /**
   * Update sound settings
   */
  updateSettings(newSettings: Partial<SoundSettings>): void {
    const currentSettings = this.settingsSubject.value;
    const updatedSettings = { ...currentSettings, ...newSettings };

    this.settingsSubject.next(updatedSettings);
    this.saveSettings(updatedSettings);
  }

  /**
   * Toggle sound on/off
   */
  toggleSound(enabled?: boolean): void {
    const currentSettings = this.settingsSubject.value;
    const newEnabled = enabled !== undefined ? enabled : !currentSettings.enabled;

    this.updateSettings({ enabled: newEnabled });
  }

  /**
   * Set volume (0.0 to 1.0)
   */
  setVolume(volume: number): void {
    const clampedVolume = Math.max(0, Math.min(1, volume));
    this.updateSettings({ volume: clampedVolume });
  }

  /**
   * Get current settings
   */
  getCurrentSettings(): SoundSettings {
    return this.settingsSubject.value;
  }

  /**
   * Check if sounds are enabled
   */
  isEnabled(): boolean {
    return this.settingsSubject.value.enabled;
  }

  /**
   * Check if audio is supported and initialized
   */
  isSupported(): boolean {
    return this.isInitialized && this.audioContext !== null;
  }

  /**
   * Load settings from localStorage
   */
  private loadSettings(): void {
    try {
      const savedSettings = localStorage.getItem(this.STORAGE_KEY);
      if (savedSettings) {
        const parsedSettings = JSON.parse(savedSettings);
        const mergedSettings = { ...this.defaultSettings, ...parsedSettings };
        this.settingsSubject.next(mergedSettings);
      }
    } catch (error) {
      console.warn('Failed to load sound settings:', error);
    }
  }

  /**
   * Save settings to localStorage
   */
  private saveSettings(settings: SoundSettings): void {
    try {
      localStorage.setItem(this.STORAGE_KEY, JSON.stringify(settings));
    } catch (error) {
      console.warn('Failed to save sound settings:', error);
    }
  }

  /**
   * Reset settings to default
   */
  resetSettings(): void {
    this.settingsSubject.next(this.defaultSettings);
    this.saveSettings(this.defaultSettings);
  }
}