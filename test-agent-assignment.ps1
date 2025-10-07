# Test Agent Assignment System
$baseUrl = "http://localhost:5000"

Write-Host "=== Testing Agent Assignment System ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Login to get JWT token
Write-Host "Step 1: Logging in as test@admin.com..." -ForegroundColor Yellow
$loginBody = @{
    email = "test@admin.com"
    password = "Password123!"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.data.token
    Write-Host "Success: Login successful!" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "Error: Login failed - $_" -ForegroundColor Red
    exit 1
}

# Setup headers with JWT token
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Step 2: Test Assignment Statistics
Write-Host "Step 2: Testing GET /api/assignments/statistics" -ForegroundColor Yellow
try {
    $stats = Invoke-RestMethod -Uri "$baseUrl/api/assignments/statistics" -Method Get -Headers $headers
    Write-Host "Success: Statistics retrieved!" -ForegroundColor Green
    Write-Host ($stats | ConvertTo-Json -Depth 3) -ForegroundColor Gray
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
Write-Host ""

# Step 3: Test Active Assignments
Write-Host "Step 3: Testing GET /api/assignments/active" -ForegroundColor Yellow
try {
    $active = Invoke-RestMethod -Uri "$baseUrl/api/assignments/active" -Method Get -Headers $headers
    Write-Host "Success: Found $($active.Count) active assignments" -ForegroundColor Green
    if ($active.Count -gt 0) {
        Write-Host ($active | ConvertTo-Json -Depth 2) -ForegroundColor Gray
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
Write-Host ""

# Step 4: Test Agent Performance
Write-Host "Step 4: Testing GET /api/assignments/agent-performance" -ForegroundColor Yellow
try {
    $performance = Invoke-RestMethod -Uri "$baseUrl/api/assignments/agent-performance" -Method Get -Headers $headers
    Write-Host "Success: Agent performance retrieved!" -ForegroundColor Green
    Write-Host ($performance | ConvertTo-Json -Depth 2) -ForegroundColor Gray
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
Write-Host ""

# Step 5: Test Assignment History
Write-Host "Step 5: Testing GET /api/assignments/history" -ForegroundColor Yellow
try {
    $history = Invoke-RestMethod -Uri "$baseUrl/api/assignments/history" -Method Get -Headers $headers
    Write-Host "Success: Found $($history.Count) historical assignments" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Test Complete ===" -ForegroundColor Cyan
