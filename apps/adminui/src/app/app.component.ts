import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeModeService } from './core/services/theme-mode.service';
import { NetworkStatusService } from './core/services/network-status.service';
import { BackgroundSyncService } from './core/services/background-sync.service';
import { ServiceWorkerUpdateService } from './core/services/service-worker-update.service';
import { PwaInstallService } from './core/services/pwa-install.service';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'StayBOT Admin';

  constructor(
    private themeModeService: ThemeModeService,
    private networkStatusService: NetworkStatusService,
    private backgroundSyncService: BackgroundSyncService,
    private swUpdateService: ServiceWorkerUpdateService,
    private pwaInstallService: PwaInstallService
  ) {}

  async ngOnInit(): Promise<void> {
    // Initialize PWA services if enabled
    if (environment.pwa?.enabled) {
      console.log('Initializing PWA services...');

      try {
        // Initialize background sync service
        await this.backgroundSyncService.initialize();
        console.log('Background sync initialized');

        // Initialize service worker update service
        await this.swUpdateService.initialize();
        console.log('Service worker update monitoring initialized');

        // Monitor network status
        this.networkStatusService.getNetworkStatus().subscribe(status => {
          console.log('Network status:', status);
        });

        // Monitor for PWA install prompt
        this.pwaInstallService.getInstallPromptState().subscribe(state => {
          if (state.canInstall && !state.isInstalled) {
            console.log('PWA can be installed');
          }
        });

        console.log('All PWA services initialized successfully');
      } catch (error) {
        console.error('Error initializing PWA services:', error);
      }
    }
  }
}
