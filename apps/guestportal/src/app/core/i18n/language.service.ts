import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject } from 'rxjs';

export interface Language {
  code: string;
  name: string;
  nativeName: string;
  flag: string;
}

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private translate = inject(TranslateService);
  private readonly STORAGE_KEY = 'guestportal_language';

  public readonly supportedLanguages: Language[] = [
    { code: 'en', name: 'English', nativeName: 'English', flag: '🇬🇧' },
    { code: 'af', name: 'Afrikaans', nativeName: 'Afrikaans', flag: '🇿🇦' },
    { code: 'zu', name: 'Zulu', nativeName: 'isiZulu', flag: '🇿🇦' },
    { code: 'fr', name: 'French', nativeName: 'Français', flag: '🇫🇷' },
    { code: 'de', name: 'German', nativeName: 'Deutsch', flag: '🇩🇪' },
    { code: 'zh', name: 'Chinese', nativeName: '中文', flag: '🇨🇳' }
  ];

  private _currentLanguage$ = new BehaviorSubject<Language>(this.supportedLanguages[0]);
  public currentLanguage$ = this._currentLanguage$.asObservable();

  constructor() {
    this.initLanguage();
  }

  /**
   * Initialize language from storage or browser
   */
  private initLanguage(): void {
    // Set available languages
    const langCodes = this.supportedLanguages.map(l => l.code);
    this.translate.addLangs(langCodes);
    this.translate.setDefaultLang('en');

    // Try to restore from storage
    const stored = localStorage.getItem(this.STORAGE_KEY);
    if (stored && langCodes.includes(stored)) {
      this.setLanguage(stored);
      return;
    }

    // Try to detect from browser
    const browserLang = this.translate.getBrowserLang();
    if (browserLang && langCodes.includes(browserLang)) {
      this.setLanguage(browserLang);
      return;
    }

    // Default to English
    this.setLanguage('en');
  }

  /**
   * Set current language
   */
  setLanguage(code: string): void {
    const lang = this.supportedLanguages.find(l => l.code === code);
    if (lang) {
      this.translate.use(code);
      this._currentLanguage$.next(lang);
      localStorage.setItem(this.STORAGE_KEY, code);
    }
  }

  /**
   * Get current language synchronously
   */
  getCurrentLanguage(): Language {
    return this._currentLanguage$.getValue();
  }

  /**
   * Get language by code
   */
  getLanguage(code: string): Language | undefined {
    return this.supportedLanguages.find(l => l.code === code);
  }
}
