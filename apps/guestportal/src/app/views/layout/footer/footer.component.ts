import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { TenantService, TenantInfo } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    <footer class="footer">
      <div class="container">
        <div class="footer-content">
          <!-- Social Links -->
          @if (tenant?.socialLinks) {
            <div class="social-links">
              @if (tenant?.socialLinks?.facebook) {
                <a [href]="tenant?.socialLinks?.facebook" target="_blank" class="social-link" aria-label="Facebook">
                  <i class="bi bi-facebook"></i>
                </a>
              }
              @if (tenant?.socialLinks?.instagram) {
                <a [href]="tenant?.socialLinks?.instagram" target="_blank" class="social-link" aria-label="Instagram">
                  <i class="bi bi-instagram"></i>
                </a>
              }
              @if (tenant?.socialLinks?.twitter) {
                <a [href]="tenant?.socialLinks?.twitter" target="_blank" class="social-link" aria-label="Twitter">
                  <i class="bi bi-twitter-x"></i>
                </a>
              }
              @if (tenant?.socialLinks?.website) {
                <a [href]="tenant?.socialLinks?.website" target="_blank" class="social-link" aria-label="Website">
                  <i class="bi bi-globe"></i>
                </a>
              }
            </div>
          }

          <!-- Copyright -->
          <div class="copyright">
            <p class="hotel-name">&copy; {{ currentYear }} {{ tenant?.name || 'Guest Portal' }}</p>
            <p class="powered-by">Powered by <strong>StayBot</strong></p>
          </div>
        </div>
      </div>
    </footer>
  `,
  styles: [`
    .footer {
      background: rgba(255, 255, 255, 0.85);
      backdrop-filter: blur(20px);
      -webkit-backdrop-filter: blur(20px);
      padding: 1.5rem 0;
      margin-top: auto;
      border-top: 1px solid rgba(255, 255, 255, 0.3);
    }

    .footer-content {
      text-align: center;
    }

    .social-links {
      display: flex;
      justify-content: center;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .social-link {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 44px;
      height: 44px;
      border-radius: 50%;
      background: #1a1a1a;
      color: white;
      text-decoration: none;
      transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .social-link:hover {
      transform: translateY(-3px) scale(1.05);
      box-shadow: 0 8px 20px rgba(0, 0, 0, 0.2);
    }

    .social-link i {
      font-size: 1.1rem;
    }

    .copyright {
      color: #666;
    }

    .hotel-name {
      font-size: 0.85rem;
      margin-bottom: 0.25rem;
      font-weight: 500;
      color: #333;
    }

    .powered-by {
      font-size: 0.75rem;
      margin: 0;
      opacity: 0.7;
    }

    .powered-by strong {
      color: #1a1a1a;
    }
  `]
})
export class FooterComponent {
  private tenantService = inject(TenantService);

  tenant: TenantInfo | null = null;
  currentYear = new Date().getFullYear();

  constructor() {
    this.tenantService.tenant$.subscribe(tenant => {
      this.tenant = tenant;
    });
  }
}
