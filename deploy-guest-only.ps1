# Deploy Guest Portal to Azure
# Resource configuration
$resourceGroup = "staybot-prod-rg"
$appName = "staybot-guest"
$projectPath = "C:\Users\Administrator\Downloads\hostr\apps\guestportal"

Write-Host "=== StayBot Guest Portal Deployment ===" -ForegroundColor Green

# Step 1: Build Angular app
Write-Host "`n[1/3] Building Angular application..." -ForegroundColor Cyan
cd $projectPath
npm run build -- --configuration production

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Step 2: Create deployment package
Write-Host "`n[2/3] Creating deployment package..." -ForegroundColor Cyan
$distPath = Join-Path $projectPath "dist\guestportal\browser"

if (Test-Path "$projectPath\guest-deploy.zip") {
    Remove-Item "$projectPath\guest-deploy.zip" -Force
}

Compress-Archive -Path "$distPath\*" -DestinationPath "$projectPath\guest-deploy.zip" -Force

# Step 3: Deploy to Azure
Write-Host "`n[3/3] Deploying to Azure..." -ForegroundColor Cyan
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $appName `
  --src "$projectPath\guest-deploy.zip"

Write-Host "`n=== Deployment Complete! ===" -ForegroundColor Green
Write-Host "Guest Portal URL: https://$appName.azurewebsites.net" -ForegroundColor Cyan
