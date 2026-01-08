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
          <div class="social-links">
            @if (tenant?.socialLinks?.instagram) {
              <a [href]="tenant?.socialLinks?.instagram" target="_blank" class="social-link" aria-label="Instagram">
                <i class="bi bi-instagram"></i>
              </a>
            }
            @if (tenant?.socialLinks?.facebook) {
              <a [href]="tenant?.socialLinks?.facebook" target="_blank" class="social-link" aria-label="Facebook">
                <i class="bi bi-facebook"></i>
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
            <!-- Show placeholder icons if no social links configured -->
            @if (!hasSocialLinks) {
              <span class="social-link placeholder" aria-label="Instagram">
                <i class="bi bi-instagram"></i>
              </span>
              <span class="social-link placeholder" aria-label="Facebook">
                <i class="bi bi-facebook"></i>
              </span>
              <span class="social-link placeholder" aria-label="Twitter">
                <i class="bi bi-twitter-x"></i>
              </span>
              <span class="social-link placeholder" aria-label="Website">
                <i class="bi bi-globe"></i>
              </span>
            }
          </div>

          <!-- Copyright -->
          <div class="copyright">
            <p class="powered-by">Powered by <strong>StayBot</strong></p>
          </div>
        </div>
      </div>
    </footer>
  `,
  styles: [`
    .footer {
      background: transparent;
      padding: 2rem 0 1.5rem;
      margin-top: auto;
    }

    .footer-content {
      text-align: center;
    }

    .social-links {
      display: flex;
      justify-content: center;
      gap: 0.75rem;
      margin-bottom: 1.25rem;
    }

    .social-link {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      border-radius: 12px;
      background: #1a1a1a;
      color: white;
      text-decoration: none;
      transition: all 0.3s ease;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
    }

    .social-link:hover {
      transform: translateY(-3px) scale(1.05);
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3);
      color: white;
    }

    .social-link.placeholder {
      opacity: 0.6;
      cursor: default;
    }

    .social-link.placeholder:hover {
      transform: none;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
    }

    .social-link i {
      font-size: 1.25rem;
    }

    .copyright {
      color: rgba(255, 255, 255, 0.7);
    }

    .powered-by {
      font-size: 0.8rem;
      margin: 0;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .powered-by strong {
      color: white;
    }
  `]
})
export class FooterComponent {
  private tenantService = inject(TenantService);

  tenant: TenantInfo | null = null;

  get hasSocialLinks(): boolean {
    return !!(this.tenant?.socialLinks?.instagram ||
              this.tenant?.socialLinks?.facebook ||
              this.tenant?.socialLinks?.twitter ||
              this.tenant?.socialLinks?.website);
  }

  constructor() {
    this.tenantService.tenant$.subscribe(tenant => {
      this.tenant = tenant;
    });
  }
}
