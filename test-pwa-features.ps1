# PWA Feature Testing Script
# Tests all PWA functionality including offline storage, service worker, and push notifications

param(
    [string]$BaseUrl = "http://localhost:4200",
    [string]$ApiUrl = "http://localhost:5000",
    [switch]$SkipBuildCheck,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$testsPassed = 0
$testsFailed = 0
$testsSkipped = 0

# Color coding functions
function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Write-TestPass {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
    $script:testsPassed++
}

function Write-TestFail {
    param([string]$Message)
    Write-Host "[X] $Message" -ForegroundColor Red
    $script:testsFailed++
}

function Write-TestSkip {
    param([string]$Message)
    Write-Host "⊘ $Message" -ForegroundColor Yellow
    $script:testsSkipped++
}

function Write-TestInfo {
    param([string]$Message)
    Write-Host "  → $Message" -ForegroundColor Gray
}

# Test API availability
function Test-ApiAvailability {
    Write-TestHeader "Testing API Availability"

    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/health" -Method GET -TimeoutSec 5 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-TestPass "API is running at $ApiUrl"
            return $true
        }
    } catch {
        Write-TestFail "API is not accessible at $ApiUrl"
        Write-TestInfo "Start the API: cd apps/api && dotnet run --urls=http://localhost:5000"
        return $false
    }
}

# Test production build exists
function Test-ProductionBuild {
    Write-TestHeader "Testing Production Build"

    $distPath = "apps\adminui\dist\staybot-admin"

    if (Test-Path $distPath) {
        Write-TestPass "Production build directory exists: $distPath"

        # Check for key files
        $requiredFiles = @(
            "index.html",
            "ngsw-worker.js",
            "ngsw.json",
            "manifest.webmanifest"
        )

        $allFilesExist = $true
        foreach ($file in $requiredFiles) {
            $filePath = Join-Path $distPath $file
            if (Test-Path $filePath) {
                Write-TestPass "Found: $file"
            } else {
                Write-TestFail "Missing: $file"
                $allFilesExist = $false
            }
        }

        return $allFilesExist
    } else {
        Write-TestFail "Production build not found at: $distPath"
        Write-TestInfo "Build the app: cd apps/adminui && npm run build -- --configuration production"
        return $false
    }
}

# Test service worker configuration
function Test-ServiceWorkerConfig {
    Write-TestHeader "Testing Service Worker Configuration"

    $ngsWConfigPath = "apps\adminui\dist\staybot-admin\ngsw.json"

    if (Test-Path $ngsWConfigPath) {
        Write-TestPass "Service worker config exists"

        try {
            $config = Get-Content $ngsWConfigPath -Raw | ConvertFrom-Json

            if ($config.index) {
                Write-TestPass "Index file configured: $($config.index)"
            }

            if ($config.assetGroups) {
                Write-TestPass "Asset groups configured: $($config.assetGroups.Count) groups"
            }

            if ($config.dataGroups) {
                Write-TestPass "Data groups configured: $($config.dataGroups.Count) groups"
            }

            return $true
        } catch {
            Write-TestFail "Failed to parse service worker config: $_"
            return $false
        }
    } else {
        Write-TestFail "Service worker config not found"
        return $false
    }
}

# Test PWA manifest
function Test-ManifestConfig {
    Write-TestHeader "Testing PWA Manifest"

    $manifestPath = "apps\adminui\dist\staybot-admin\manifest.webmanifest"

    if (Test-Path $manifestPath) {
        Write-TestPass "Manifest file exists"

        try {
            $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

            $requiredFields = @{
                "name" = "StayBOT Admin"
                "short_name" = "StayBOT"
                "display" = "standalone"
                "orientation" = "portrait"
            }

            foreach ($field in $requiredFields.Keys) {
                if ($manifest.$field -eq $requiredFields[$field]) {
                    Write-TestPass "Manifest.$field = '$($manifest.$field)'"
                } else {
                    Write-TestFail "Manifest.$field mismatch. Expected: '$($requiredFields[$field])', Got: '$($manifest.$field)'"
                }
            }

            if ($manifest.icons -and $manifest.icons.Count -ge 8) {
                Write-TestPass "Manifest has $($manifest.icons.Count) icons configured"
            } else {
                Write-TestFail "Manifest missing icons (expected 8+)"
            }

            return $true
        } catch {
            Write-TestFail "Failed to parse manifest: $_"
            return $false
        }
    } else {
        Write-TestFail "Manifest file not found"
        return $false
    }
}

# Test PWA icons
function Test-PwaIcons {
    Write-TestHeader "Testing PWA Icons"

    $iconSizes = @("72x72", "96x96", "128x128", "144x144", "152x152", "192x192", "384x384", "512x512")
    $iconsPath = "apps\adminui\dist\staybot-admin\icons"

    if (Test-Path $iconsPath) {
        $allIconsExist = $true
        foreach ($size in $iconSizes) {
            $iconPath = Join-Path $iconsPath "icon-$size.png"
            if (Test-Path $iconPath) {
                Write-TestPass "Found icon: icon-$size.png"
            } else {
                Write-TestFail "Missing icon: icon-$size.png"
                $allIconsExist = $false
            }
        }
        return $allIconsExist
    } else {
        Write-TestFail "Icons directory not found: $iconsPath"
        return $false
    }
}

# Test IndexedDB service
function Test-IndexedDbService {
    Write-TestHeader "Testing IndexedDB Service"

    $indexedDbServicePath = "apps\adminui\src\app\core\services\indexed-db.service.ts"

    if (Test-Path $indexedDbServicePath) {
        Write-TestPass "IndexedDB service exists"

        $content = Get-Content $indexedDbServicePath -Raw

        # Check for version 2
        if ($content -match "version:\s*2") {
            Write-TestPass "IndexedDB version set to 2 (supports offline actions)"
        } else {
            Write-TestFail "IndexedDB version not updated to 2"
        }

        # Check for required stores
        $requiredStores = @("tasks", "conversations", "messages", "emergencies", "offlineActions")
        foreach ($store in $requiredStores) {
            if ($content -match "name:\s*['""]$store['""]") {
                Write-TestPass "Object store configured: $store"
            } else {
                Write-TestFail "Object store missing: $store"
            }
        }

        return $true
    } else {
        Write-TestFail "IndexedDB service not found"
        return $false
    }
}

# Test PWA services
function Test-PwaServices {
    Write-TestHeader "Testing PWA Services"

    $servicesPath = "apps\adminui\src\app\core\services"

    $requiredServices = @(
        "network-status.service.ts",
        "offline-action-queue.service.ts",
        "background-sync.service.ts",
        "service-worker-update.service.ts",
        "pwa-install.service.ts",
        "push-notification.service.ts"
    )

    $allServicesExist = $true
    foreach ($service in $requiredServices) {
        $servicePath = Join-Path $servicesPath $service
        if (Test-Path $servicePath) {
            Write-TestPass "Found service: $service"
        } else {
            Write-TestFail "Missing service: $service"
            $allServicesExist = $false
        }
    }

    return $allServicesExist
}

# Test VAPID configuration
function Test-VapidConfiguration {
    Write-TestHeader "Testing VAPID Configuration"

    # Check frontend environment files
    $envFiles = @(
        "apps\adminui\src\environments\environment.ts",
        "apps\adminui\src\environments\environment.prod.ts"
    )

    $allConfigured = $true
    foreach ($envFile in $envFiles) {
        if (Test-Path $envFile) {
            $content = Get-Content $envFile -Raw
            if ($content -match "vapidPublicKey:\s*['""]BCtM1kh-NFEV") {
                Write-TestPass "VAPID public key configured in: $(Split-Path $envFile -Leaf)"
            } else {
                Write-TestFail "VAPID public key missing in: $(Split-Path $envFile -Leaf)"
                $allConfigured = $false
            }
        } else {
            Write-TestFail "Environment file not found: $envFile"
            $allConfigured = $false
        }
    }

    # Check backend appsettings
    $appsettingsPath = "apps\api\appsettings.json"
    if (Test-Path $appsettingsPath) {
        $content = Get-Content $appsettingsPath -Raw
        if ($content -match '"WebPush"') {
            Write-TestPass "WebPush configuration found in appsettings.json"

            if ($content -match '"PublicKey"' -and $content -match '"PrivateKey"') {
                Write-TestPass "VAPID keys configured in backend"
            } else {
                Write-TestFail "VAPID keys incomplete in backend"
                $allConfigured = $false
            }
        } else {
            Write-TestFail "WebPush configuration missing in appsettings.json"
            $allConfigured = $false
        }
    }

    return $allConfigured
}

# Test backend push notification API
function Test-PushNotificationApi {
    Write-TestHeader "Testing Push Notification API"

    $controllerPath = "apps\api\Controllers\PushNotificationController.cs"
    $modelPath = "apps\api\Models\PushSubscription.cs"

    $allFilesExist = $true

    if (Test-Path $controllerPath) {
        Write-TestPass "PushNotificationController exists"

        $content = Get-Content $controllerPath -Raw

        # Check for required endpoints
        $endpoints = @(
            @{Name="Subscribe"; Pattern="HttpPost.*subscribe"},
            @{Name="Unsubscribe"; Pattern="HttpPost.*unsubscribe"},
            @{Name="GetSubscriptions"; Pattern="HttpGet.*subscriptions"},
            @{Name="SendNotification"; Pattern="HttpPost.*send"},
            @{Name="DeleteSubscription"; Pattern="HttpDelete.*subscriptions"}
        )

        foreach ($endpoint in $endpoints) {
            if ($content -match $endpoint.Pattern) {
                Write-TestPass "Endpoint configured: $($endpoint.Name)"
            } else {
                Write-TestFail "Endpoint missing: $($endpoint.Name)"
                $allFilesExist = $false
            }
        }
    } else {
        Write-TestFail "PushNotificationController not found"
        $allFilesExist = $false
    }

    if (Test-Path $modelPath) {
        Write-TestPass "PushSubscription model exists"
    } else {
        Write-TestFail "PushSubscription model not found"
        $allFilesExist = $false
    }

    return $allFilesExist
}

# Test database migration
function Test-DatabaseMigration {
    Write-TestHeader "Testing Database Migration"

    $migrationsPath = "apps\api\Migrations"

    if (Test-Path $migrationsPath) {
        # Check for PushSubscriptions migration
        $migrationFiles = Get-ChildItem -Path $migrationsPath -Filter "*AddPushSubscriptions*"

        if ($migrationFiles.Count -gt 0) {
            Write-TestPass "PushSubscriptions migration found: $($migrationFiles[0].Name)"

            # Check if migration was applied (check snapshot)
            $snapshotPath = Join-Path $migrationsPath "HostrDbContextModelSnapshot.cs"
            if (Test-Path $snapshotPath) {
                $snapshot = Get-Content $snapshotPath -Raw
                if ($snapshot -match "PushSubscriptions") {
                    Write-TestPass "PushSubscriptions table exists in model snapshot"
                    return $true
                } else {
                    Write-TestFail "PushSubscriptions not in model snapshot (migration may not be applied)"
                    Write-TestInfo "Apply migration: cd apps/api; dotnet ef database update"
                    return $false
                }
            }
        } else {
            Write-TestFail "PushSubscriptions migration not found"
            Write-TestInfo "Create migration: cd apps/api; dotnet ef migrations add AddPushSubscriptions"
            return $false
        }
    } else {
        Write-TestFail "Migrations directory not found"
        return $false
    }
}

# Test app component integration
function Test-AppComponentIntegration {
    Write-TestHeader "Testing App Component Integration"

    $appComponentPath = "apps\adminui\src\app\app.component.ts"

    if (Test-Path $appComponentPath) {
        Write-TestPass "App component exists"

        $content = Get-Content $appComponentPath -Raw

        # Check for PWA service injections
        $services = @(
            "BackgroundSyncService",
            "ServiceWorkerUpdateService",
            "NetworkStatusService"
        )

        $allIntegrated = $true
        foreach ($service in $services) {
            if ($content -match $service) {
                Write-TestPass "Service integrated: $service"
            } else {
                Write-TestFail "Service not integrated: $service"
                $allIntegrated = $false
            }
        }

        # Check for PWA initialization
        if ($content -match "environment\.pwa\?\.enabled") {
            Write-TestPass "PWA initialization code present"
        } else {
            Write-TestFail "PWA initialization code missing"
            $allIntegrated = $false
        }

        return $allIntegrated
    } else {
        Write-TestFail "App component not found"
        return $false
    }
}

# Main test execution
function Invoke-PwaTests {
    Write-Host @"
╔════════════════════════════════════════════════════════════════╗
║                  PWA Feature Testing Suite                     ║
║                    StayBOT Admin Application                   ║
╚════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

    Write-Host "`nBase URL: $BaseUrl" -ForegroundColor White
    Write-Host "API URL: $ApiUrl" -ForegroundColor White
    Write-Host ""

    # Run all tests
    $apiRunning = Test-ApiAvailability

    if (-not $SkipBuildCheck) {
        Test-ProductionBuild
        Test-ServiceWorkerConfig
        Test-ManifestConfig
        Test-PwaIcons
    } else {
        Write-TestSkip "Production build checks (--SkipBuildCheck flag used)"
    }

    Test-IndexedDbService
    Test-PwaServices
    Test-VapidConfiguration
    Test-PushNotificationApi
    Test-DatabaseMigration
    Test-AppComponentIntegration

    # Summary
    Write-TestHeader "Test Summary"
    Write-Host ""
    Write-Host "Tests Passed:  " -NoNewline
    Write-Host $testsPassed -ForegroundColor Green
    Write-Host "Tests Failed:  " -NoNewline
    Write-Host $testsFailed -ForegroundColor Red
    Write-Host "Tests Skipped: " -NoNewline
    Write-Host $testsSkipped -ForegroundColor Yellow
    Write-Host ""

    $totalTests = $testsPassed + $testsFailed
    if ($totalTests -gt 0) {
        $successRate = [math]::Round(($testsPassed / $totalTests) * 100, 2)
        Write-Host "Success Rate: $successRate%" -ForegroundColor $(if ($successRate -ge 90) { "Green" } elseif ($successRate -ge 70) { "Yellow" } else { "Red" })
    }

    # Manual testing instructions
    if ($testsFailed -eq 0) {
        Write-Host "`n✓ All automated tests passed!" -ForegroundColor Green
        Write-Host "`nNext Steps - Manual Browser Testing:" -ForegroundColor Cyan
        Write-Host "1. Serve the production build:" -ForegroundColor White
        Write-Host "   cd apps/adminui/dist/staybot-admin" -ForegroundColor Gray
        Write-Host "   npx http-server -p 4200 -c-1" -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Open Chrome and navigate to: http://localhost:4200" -ForegroundColor White
        Write-Host ""
        Write-Host "3. Test Service Worker (DevTools → Application → Service Workers):" -ForegroundColor White
        Write-Host "   - Should see 'ngsw-worker.js' as activated" -ForegroundColor Gray
        Write-Host ""
        Write-Host "4. Test Offline Mode (DevTools → Network → Throttling: Offline):" -ForegroundColor White
        Write-Host "   - Navigate to Tasks page while online" -ForegroundColor Gray
        Write-Host "   - Go offline" -ForegroundColor Gray
        Write-Host "   - Refresh page - should still work" -ForegroundColor Gray
        Write-Host ""
        Write-Host "5. Test IndexedDB (DevTools → Application → IndexedDB):" -ForegroundColor White
        Write-Host "   - Should see 'staybot-admin-db' with 5 object stores" -ForegroundColor Gray
        Write-Host "   - Check 'offlineActions' store for queued actions" -ForegroundColor Gray
        Write-Host ""
        Write-Host "6. Test Push Notifications (Console):" -ForegroundColor White
        Write-Host "   - Request permission and subscribe" -ForegroundColor Gray
        Write-Host "   - Test notification using API endpoint" -ForegroundColor Gray
        Write-Host ""
        Write-Host "For detailed testing guide, see: PWA_TESTING_GUIDE.md" -ForegroundColor Cyan
    } else {
        Write-Host "`n[X] Some tests failed. Please fix the issues above before manual testing." -ForegroundColor Red
    }

    Write-Host ""
}

# Run the tests
Invoke-PwaTests
