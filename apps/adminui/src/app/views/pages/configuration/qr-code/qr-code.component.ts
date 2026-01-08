import { Component, OnInit, inject, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../../../core/services/auth.service';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { environment } from '../../../../../environments/environment';
import QRCode from 'qrcode';

interface PortalSettings {
  logoUrl?: string;
  backgroundImageUrl?: string;
  whatsappNumber?: string;
  guestPortalEnabled?: boolean;
}

@Component({
  selector: 'app-qr-code',
  standalone: true,
  imports: [CommonModule, FormsModule, FeatherIconDirective],
  templateUrl: './qr-code.component.html',
  styleUrls: ['./qr-code.component.scss']
})
export class QrCodeComponent implements OnInit {
  @ViewChild('qrCanvas', { static: false }) qrCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('downloadCanvas', { static: false }) downloadCanvas!: ElementRef<HTMLCanvasElement>;

  private authService = inject(AuthService);
  private http = inject(HttpClient);

  tenantSlug: string = '';
  tenantName: string = '';
  logoUrl: string = '';
  qrCodeUrl: string = '';

  // Customization options
  qrSize: number = 300;
  showLogo: boolean = true;
  headerText: string = 'SCAN FOR SERVICES';
  subText: string = 'Access hotel services instantly';

  loading: boolean = false;
  generated: boolean = false;

  ngOnInit(): void {
    const tenant = this.authService.currentTenantValue;
    if (tenant) {
      this.tenantSlug = tenant.slug || '';
      this.tenantName = tenant.name || '';
      this.qrCodeUrl = `https://${this.tenantSlug}.staybot.co.za`;
    }

    // Load logo from portal settings
    this.loadPortalSettings();
  }

  private loadPortalSettings(): void {
    this.http.get<PortalSettings>(`${environment.apiUrl}/tenant/portal-settings`)
      .subscribe({
        next: (settings) => {
          this.logoUrl = settings.logoUrl || '';
        },
        error: (err) => {
          console.error('Error loading portal settings:', err);
        }
      });
  }

  async generateQRCode(): Promise<void> {
    if (!this.qrCanvas || !this.tenantSlug) return;

    this.loading = true;
    const canvas = this.qrCanvas.nativeElement;

    try {
      // Generate QR code
      await QRCode.toCanvas(canvas, this.qrCodeUrl, {
        width: this.qrSize,
        margin: 2,
        color: {
          dark: '#1a1a2e',
          light: '#ffffff'
        },
        errorCorrectionLevel: 'H' // High error correction for logo overlay
      });

      // Add logo overlay if enabled and logo exists
      if (this.showLogo && this.logoUrl) {
        await this.addLogoToQR(canvas);
      }

      this.generated = true;
    } catch (error) {
      console.error('Error generating QR code:', error);
    } finally {
      this.loading = false;
    }
  }

  private async addLogoToQR(canvas: HTMLCanvasElement): Promise<void> {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    return new Promise((resolve, reject) => {
      const logo = new Image();
      logo.crossOrigin = 'anonymous';

      logo.onload = () => {
        const logoSize = this.qrSize * 0.22; // Logo is 22% of QR size
        const logoX = (this.qrSize - logoSize) / 2;
        const logoY = (this.qrSize - logoSize) / 2;

        // Draw white circle background for logo
        ctx.beginPath();
        ctx.arc(this.qrSize / 2, this.qrSize / 2, logoSize / 2 + 8, 0, Math.PI * 2);
        ctx.fillStyle = '#ffffff';
        ctx.fill();

        // Draw logo
        ctx.save();
        ctx.beginPath();
        ctx.arc(this.qrSize / 2, this.qrSize / 2, logoSize / 2, 0, Math.PI * 2);
        ctx.clip();
        ctx.drawImage(logo, logoX, logoY, logoSize, logoSize);
        ctx.restore();

        resolve();
      };

      logo.onerror = () => {
        console.warn('Could not load logo, generating QR without it');
        resolve();
      };

      logo.src = this.logoUrl;
    });
  }

  async downloadQRCode(format: 'png' | 'pdf'): Promise<void> {
    if (!this.generated) return;

    const downloadCanvas = this.downloadCanvas.nativeElement;
    const ctx = downloadCanvas.getContext('2d');
    if (!ctx) return;

    // Set canvas size for download (larger for print quality)
    const downloadSize = 800;
    const padding = 60;
    const headerHeight = 120;
    const footerHeight = 80;
    const totalHeight = downloadSize + headerHeight + footerHeight + padding * 2;

    downloadCanvas.width = downloadSize + padding * 2;
    downloadCanvas.height = totalHeight;

    // Fill background
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, downloadCanvas.width, downloadCanvas.height);

    // Draw header text
    ctx.fillStyle = '#1a1a2e';
    ctx.font = 'bold 48px Arial, sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(this.headerText, downloadCanvas.width / 2, padding + 50);

    // Draw sub text
    ctx.font = '24px Arial, sans-serif';
    ctx.fillStyle = '#666666';
    ctx.fillText(this.subText, downloadCanvas.width / 2, padding + 90);

    // Generate high-res QR code for download
    const tempCanvas = document.createElement('canvas');
    await QRCode.toCanvas(tempCanvas, this.qrCodeUrl, {
      width: downloadSize,
      margin: 2,
      color: {
        dark: '#1a1a2e',
        light: '#ffffff'
      },
      errorCorrectionLevel: 'H'
    });

    // Add logo to temp canvas
    if (this.showLogo && this.logoUrl) {
      await this.addLogoToCanvas(tempCanvas, downloadSize);
    }

    // Draw QR code
    ctx.drawImage(tempCanvas, padding, headerHeight + padding);

    // Draw footer with URL
    ctx.font = '20px Arial, sans-serif';
    ctx.fillStyle = '#888888';
    ctx.fillText(this.qrCodeUrl, downloadCanvas.width / 2, totalHeight - padding + 20);

    // Draw hotel name
    ctx.font = 'bold 24px Arial, sans-serif';
    ctx.fillStyle = '#1a1a2e';
    ctx.fillText(this.tenantName, downloadCanvas.width / 2, totalHeight - padding - 20);

    // Download
    if (format === 'png') {
      const link = document.createElement('a');
      link.download = `${this.tenantSlug}-qr-code.png`;
      link.href = downloadCanvas.toDataURL('image/png');
      link.click();
    }
  }

  private async addLogoToCanvas(canvas: HTMLCanvasElement, size: number): Promise<void> {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    return new Promise((resolve) => {
      const logo = new Image();
      logo.crossOrigin = 'anonymous';

      logo.onload = () => {
        const logoSize = size * 0.22;
        const logoX = (size - logoSize) / 2;
        const logoY = (size - logoSize) / 2;

        ctx.beginPath();
        ctx.arc(size / 2, size / 2, logoSize / 2 + 12, 0, Math.PI * 2);
        ctx.fillStyle = '#ffffff';
        ctx.fill();

        ctx.save();
        ctx.beginPath();
        ctx.arc(size / 2, size / 2, logoSize / 2, 0, Math.PI * 2);
        ctx.clip();
        ctx.drawImage(logo, logoX, logoY, logoSize, logoSize);
        ctx.restore();

        resolve();
      };

      logo.onerror = () => resolve();
      logo.src = this.logoUrl;
    });
  }

  copyUrl(): void {
    navigator.clipboard.writeText(this.qrCodeUrl);
  }
}
