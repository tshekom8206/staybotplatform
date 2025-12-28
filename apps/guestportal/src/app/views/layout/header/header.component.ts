import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { TenantService } from '../../../core/services/tenant.service';
import { LanguageService } from '../../../core/i18n/language.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule, NgbDropdownModule],
  template: `
    <header class="header">
      <div class="container">
        <div class="header-inner">
          <!-- Logo -->
          <a routerLink="/" class="header-logo">
            @if (tenant?.logoUrl) {
              <img [src]="tenant?.logoUrl" [alt]="tenant?.name" class="logo-img">
            } @else {
              <span class="logo-text">{{ tenant?.name || 'Guest Portal' }}</span>
            }
          </a>

          <!-- Language Selector -->
          <div ngbDropdown class="language-dropdown">
            <button class="btn-language" ngbDropdownToggle>
              <span class="flag">{{ currentLanguage.flag }}</span>
              <span class="code">{{ currentLanguage.code.toUpperCase() }}</span>
              <i class="bi bi-chevron-down"></i>
            </button>
            <div ngbDropdownMenu class="dropdown-menu-end language-menu">
              @for (lang of languages; track lang.code) {
                <button ngbDropdownItem (click)="setLanguage(lang.code)"
                        [class.active]="lang.code === currentLanguage.code">
                  <span class="flag">{{ lang.flag }}</span>
                  <span>{{ lang.nativeName }}</span>
                </button>
              }
            </div>
          </div>
        </div>
      </div>
    </header>
  `,
  styles: [`
    .header {
      background: rgba(255, 255, 255, 0.85);
      backdrop-filter: blur(20px);
      -webkit-backdrop-filter: blur(20px);
      position: sticky;
      top: 0;
      z-index: 1000;
      border-bottom: 1px solid rgba(255, 255, 255, 0.3);
    }

    .header-inner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 0;
    }

    .header-logo {
      text-decoration: none;
      color: inherit;
    }

    .logo-img {
      max-height: 36px;
      width: auto;
    }

    .logo-text {
      font-size: 1.1rem;
      font-weight: 600;
      color: #1a1a1a;
      letter-spacing: -0.02em;
    }

    /* Language Button */
    .btn-language {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.5rem 0.75rem;
      background: rgba(255, 255, 255, 0.9);
      border: 1px solid rgba(0, 0, 0, 0.1);
      border-radius: 50px;
      font-size: 0.8rem;
      font-weight: 500;
      color: #1a1a1a;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .btn-language:hover {
      background: white;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }

    .btn-language .flag {
      font-size: 1rem;
    }

    .btn-language .code {
      font-size: 0.75rem;
      font-weight: 600;
    }

    .btn-language i {
      font-size: 0.65rem;
      opacity: 0.6;
    }

    /* Dropdown Menu */
    .language-menu {
      border: none;
      border-radius: 12px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
      padding: 0.5rem;
      margin-top: 0.5rem;
    }

    .language-menu button {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 0.75rem;
      border-radius: 8px;
      font-size: 0.85rem;
    }

    .language-menu button:hover {
      background: #f5f5f5;
    }

    .language-menu button.active {
      background: #1a1a1a;
      color: white;
    }

    .language-menu .flag {
      font-size: 1.1rem;
    }
  `]
})
export class HeaderComponent {
  private tenantService = inject(TenantService);
  private languageService = inject(LanguageService);

  tenant = this.tenantService.getCurrentTenant();
  languages = this.languageService.supportedLanguages;
  currentLanguage = this.languageService.getCurrentLanguage();

  constructor() {
    this.tenantService.tenant$.subscribe(tenant => {
      this.tenant = tenant;
    });

    this.languageService.currentLanguage$.subscribe(lang => {
      this.currentLanguage = lang;
    });
  }

  setLanguage(code: string): void {
    this.languageService.setLanguage(code);
  }
}
