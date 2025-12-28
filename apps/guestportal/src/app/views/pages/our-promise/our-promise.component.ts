import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, GuestPromise } from '../../../core/services/guest-api.service';

interface PromiseContent {
  promises?: string[];
  text?: string;
}

@Component({
  selector: 'app-our-promise',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header with Glassmorphism -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ promiseData()?.title || ('promise.title' | translate) }}</h1>
        </div>

        @if (loading()) {
          <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else {
          <div class="promise-content">
            @if (parsedContent()?.promises) {
              <div class="promises-list">
                @for (promise of parsedContent()!.promises; track promise; let i = $index) {
                  <div class="promise-item" [style.animation-delay]="(i * 0.1) + 's'">
                    <div class="promise-icon">
                      <i class="bi" [class]="getPromiseIcon(i)"></i>
                    </div>
                    <p>{{ promise }}</p>
                  </div>
                }
              </div>
            } @else if (parsedContent()?.text) {
              <div class="promise-text">
                <p>{{ parsedContent()!.text }}</p>
              </div>
            } @else {
              <div class="promise-text">
                <p>{{ promiseData()?.content }}</p>
              </div>
            }

            <div class="signature">
              <i class="bi bi-heart-fill"></i>
              <p>{{ 'promise.signature' | translate }}</p>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1rem 0; }

    /* Page Header - Clean, floating on background */
    .page-header {
      padding: 1.5rem 0 1.25rem;
      margin-bottom: 1rem;
    }
    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-decoration: none;
      font-size: 0.9rem;
      font-weight: 500;
      padding: 0.4rem 0.75rem;
      margin: -0.4rem -0.75rem 0.75rem;
      border-radius: 50px;
      transition: all 0.2s ease;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }
    .back-link:hover { background: rgba(255, 255, 255, 0.15); color: white; }
    .back-link i { font-size: 1rem; }
    .page-title {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }

    .loading-spinner {
      display: flex;
      justify-content: center;
      padding: 3rem;
    }

    .promise-content {
      background: white;
      border-radius: 16px;
      box-shadow: 0 4px 16px rgba(0,0,0,0.08);
      padding: 1.5rem;
      overflow: hidden;
    }

    .promises-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .promise-item {
      display: flex;
      align-items: flex-start;
      gap: 1rem;
      padding: 1rem;
      background: linear-gradient(135deg, #f8f9fa 0%, #fff 100%);
      border-radius: 12px;
      border-left: 4px solid #1a1a1a;
      animation: fadeInUp 0.5s ease forwards;
      opacity: 0;
    }

    @keyframes fadeInUp {
      from {
        opacity: 0;
        transform: translateY(10px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .promise-icon {
      width: 40px;
      height: 40px;
      background: #1a1a1a;
      color: white;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.1rem;
      flex-shrink: 0;
    }

    .promise-item p {
      margin: 0;
      font-size: 1rem;
      color: #333;
      line-height: 1.5;
      padding-top: 0.5rem;
    }

    .promise-text {
      padding: 1rem;
      line-height: 1.8;
      color: #444;
    }

    .signature {
      margin-top: 2rem;
      padding-top: 1.5rem;
      border-top: 1px solid #eee;
      text-align: center;
      color: #666;
    }

    .signature i {
      color: #e74c3c;
      font-size: 1.5rem;
      margin-bottom: 0.5rem;
    }

    .signature p {
      margin: 0;
      font-style: italic;
      font-size: 0.9rem;
    }
  `]
})
export class OurPromiseComponent implements OnInit {
  private apiService = inject(GuestApiService);

  promiseData = signal<GuestPromise | null>(null);
  parsedContent = signal<PromiseContent | null>(null);
  loading = signal(true);

  private promiseIcons = [
    'bi-heart',
    'bi-clock',
    'bi-person-check',
    'bi-chat-heart',
    'bi-star',
    'bi-shield-check',
    'bi-gem',
    'bi-hand-thumbs-up'
  ];

  ngOnInit(): void {
    this.loadPromise();
  }

  loadPromise(): void {
    this.loading.set(true);
    this.apiService.getGuestPromise().subscribe({
      next: (data) => {
        this.promiseData.set(data);
        // Try to parse the content as JSON
        try {
          if (data.content) {
            const parsed = JSON.parse(data.content);
            this.parsedContent.set(parsed);
          }
        } catch {
          // Content is not JSON, use as-is
          this.parsedContent.set({ text: data.content });
        }
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load guest promise:', error);
        this.loading.set(false);
      }
    });
  }

  getPromiseIcon(index: number): string {
    return this.promiseIcons[index % this.promiseIcons.length];
  }
}
