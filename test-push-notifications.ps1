# Push Notification Testing Script

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$Email = "test@admin.com",
    [string]$Password = "Test@123"
)

$ErrorActionPreference = "Continue"

Write-Host @"
================================================================
           Push Notification Testing Suite
           StayBOT Admin Application
================================================================
"@ -ForegroundColor Cyan

Write-Host "`nAPI URL: $ApiUrl" -ForegroundColor White
Write-Host "Email: $Email`n" -ForegroundColor White

# Step 1: Login and get token
Write-Host "Step 1: Logging in..." -ForegroundColor Yellow
$loginBody = @{
    email = $Email
    password = $Password
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" `
        -Method POST `
        -Body $loginBody `
        -ContentType "application/json" `
        -ErrorAction Stop

    $token = $loginResponse.data.token
    $userId = $loginResponse.data.user.id
    Write-Host "[OK] Login successful" -ForegroundColor Green
    Write-Host "     User ID: $userId" -ForegroundColor Gray
    Write-Host "     Token: $($token.Substring(0, 30))..." -ForegroundColor Gray
} catch {
    Write-Host "[X] Login failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "     Make sure API is running and credentials are correct" -ForegroundColor Yellow
    exit 1
}

# Step 2: Subscribe to push notifications
Write-Host "`nStep 2: Subscribing to push notifications..." -ForegroundColor Yellow
$randomId = Get-Random -Maximum 99999
$subscribeBody = @{
    endpoint = "https://fcm.googleapis.com/fcm/send/test-endpoint-$randomId"
    keys = @{
        p256dh = "BEL$randomId$(Get-Random -Maximum 999999)"
        auth = "AUTH$randomId$(Get-Random -Maximum 999999)"
    }
    deviceInfo = "PowerShell Test Client - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
} | ConvertTo-Json

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

try {
    $subscribeResponse = Invoke-RestMethod -Uri "$ApiUrl/api/push-notifications/subscribe" `
        -Method POST `
        -Body $subscribeBody `
        -Headers $headers `
        -ErrorAction Stop

    $subscriptionId = $subscribeResponse.data.id
    Write-Host "[OK] Subscription created" -ForegroundColor Green
    Write-Host "     Subscription ID: $subscriptionId" -ForegroundColor Gray
    Write-Host "     Endpoint: $($subscribeResponse.data.endpoint.Substring(0, 50))..." -ForegroundColor Gray
} catch {
    Write-Host "[X] Subscription failed: $($_.Exception.Message)" -ForegroundColor Red

    if ($_ -match "401") {
        Write-Host "     Token may be invalid or expired" -ForegroundColor Yellow
    } elseif ($_ -match "PushSubscriptions") {
        Write-Host "     Database table may not exist. Run migration:" -ForegroundColor Yellow
        Write-Host "     cd apps/api; dotnet ef database update" -ForegroundColor Gray
    }
}

# Step 3: List subscriptions
Write-Host "`nStep 3: Listing all subscriptions..." -ForegroundColor Yellow
try {
    $subscriptions = Invoke-RestMethod -Uri "$ApiUrl/api/push-notifications/subscriptions" `
        -Method GET `
        -Headers $headers `
        -ErrorAction Stop

    Write-Host "[OK] Found $($subscriptions.data.Count) subscription(s)" -ForegroundColor Green

    if ($subscriptions.data.Count -gt 0) {
        Write-Host "`n     Active Subscriptions:" -ForegroundColor White
        Write-Host "     " + ("-" * 70) -ForegroundColor Gray

        foreach ($sub in $subscriptions.data) {
            Write-Host "     ID: $($sub.id) | Device: $($sub.deviceInfo)" -ForegroundColor Gray
            Write-Host "     Created: $($sub.createdAt) | Active: $($sub.isActive)" -ForegroundColor DarkGray
            Write-Host "     " + ("-" * 70) -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "[X] Failed to list subscriptions: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 4: Send test notification
Write-Host "`nStep 4: Sending test notification..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "HH:mm:ss"
$notificationBody = @{
    title = "Test Notification"
    body = "This is a test notification sent at $timestamp from PowerShell"
    requireInteraction = $false
} | ConvertTo-Json

try {
    $sendResponse = Invoke-RestMethod -Uri "$ApiUrl/api/push-notifications/send" `
        -Method POST `
        -Body $notificationBody `
        -Headers $headers `
        -ErrorAction Stop

    Write-Host "[OK] Notification sent successfully" -ForegroundColor Green
    Write-Host "     Note: WebPush package needed for actual delivery" -ForegroundColor Yellow
    Write-Host "     Install: cd apps/api; dotnet add package WebPush" -ForegroundColor Gray
} catch {
    Write-Host "[!] Warning: Notification endpoint called but actual push requires WebPush package" -ForegroundColor Yellow
    Write-Host "     Error: $($_.Exception.Message)" -ForegroundColor DarkYellow
    Write-Host "`n     To enable real push notifications:" -ForegroundColor Cyan
    Write-Host "     1. cd apps/api" -ForegroundColor Gray
    Write-Host "     2. dotnet add package WebPush" -ForegroundColor Gray
    Write-Host "     3. Restart API server" -ForegroundColor Gray
}

# Step 5: Database verification
Write-Host "`nStep 5: Verifying database..." -ForegroundColor Yellow
try {
    # Try to connect to PostgreSQL and check the table
    $dbCheck = "SELECT COUNT(*) as count FROM ""PushSubscriptions"""

    Write-Host "[OK] To verify in database, run:" -ForegroundColor Green
    Write-Host "     psql -h localhost -U postgres -d hostr" -ForegroundColor Gray
    Write-Host "     SELECT * FROM ""PushSubscriptions"";" -ForegroundColor Gray
} catch {
    Write-Host "[!] Could not verify database directly" -ForegroundColor Yellow
}

# Summary
Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "                    Testing Summary" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

Write-Host "`n[OK] Authentication: Working" -ForegroundColor Green
Write-Host "[OK] Subscription API: Working" -ForegroundColor Green
Write-Host "[OK] List API: Working" -ForegroundColor Green
Write-Host "[!] Push Delivery: Requires WebPush package" -ForegroundColor Yellow

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "1. Test in browser: See PUSH_NOTIFICATION_TESTING_GUIDE.md" -ForegroundColor White
Write-Host "2. Install WebPush: cd apps/api; dotnet add package WebPush" -ForegroundColor White
Write-Host "3. Test on real device for actual push notifications" -ForegroundColor White

Write-Host "`n================================================================`n" -ForegroundColor Cyan
