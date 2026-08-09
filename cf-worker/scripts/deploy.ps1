# ============================================
# DualMind API — Deploy to Cloudflare Containers
# ============================================
# Prerequisites:
#   - Docker Desktop running
#   - Node.js installed
#   - Wrangler CLI: npm i -g wrangler
#   - Authenticated: wrangler login
# ============================================

param(
    [switch]$SetupSecrets,
    [switch]$SkipBuild,
    [string]$CustomDomain = "api.dualmind.arena"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CfWorkerDir = Join-Path $ProjectRoot "cf-worker"
$SrcDir = Join-Path $ProjectRoot "src"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DualMind API → Cloudflare Containers" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --------------------------------------------------
# Step 1: Install Worker dependencies
# --------------------------------------------------
Write-Host "[1/5] Installing Worker dependencies..." -ForegroundColor Yellow
Push-Location $CfWorkerDir
npm install
Pop-Location
Write-Host "  Done." -ForegroundColor Green

# --------------------------------------------------
# Step 2: Setup secrets (one-time or when rotating)
# --------------------------------------------------
if ($SetupSecrets) {
    Write-Host "[2/5] Setting up Cloudflare secrets..." -ForegroundColor Yellow
    
    $envFile = Join-Path $ProjectRoot ".env"
    if (Test-Path $envFile) {
        Write-Host "  Reading from .env file..." -ForegroundColor Gray
    }

    $secrets = @(
        "SUPABASE_URL",
        "SUPABASE_ANON_KEY",
        "SUPABASE_SERVICE_ROLE_KEY",
        "JWT_SECRET",
        "GROQ_API_KEY",
        "GOOGLE_API_KEY",
        "TELEGRAM_BOT_TOKEN",
        "CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID",
        "CLOUDFLARE_AI_GATEWAY_ID",
        "CLOUDFLARE_AI_GATEWAY_TOKEN",
        "CLOUDFLARE_WORKERS_AI_API_TOKEN"
    )

    Push-Location $CfWorkerDir
    foreach ($secret in $secrets) {
        $value = [System.Environment]::GetEnvironmentVariable($secret)
        if ([string]::IsNullOrEmpty($value) -and (Test-Path $envFile)) {
            # Try to read from .env file
            $line = Get-Content $envFile | Where-Object { $_ -match "^$secret=" }
            if ($line) {
                $value = ($line -split "=", 2)[1].Trim()
            }
        }
        
        if ([string]::IsNullOrEmpty($value)) {
            Write-Host "  ⚠ $secret not found in env - you'll need to set it manually:" -ForegroundColor Yellow
            Write-Host "    npx wrangler secret put $secret" -ForegroundColor Gray
        } else {
            Write-Host "  Setting $secret..." -ForegroundColor Gray
            $value | npx wrangler secret put $secret 2>$null
            Write-Host "  ✓ $secret set" -ForegroundColor Green
        }
    }
    Pop-Location
} else {
    Write-Host "[2/5] Skipping secrets setup (use -SetupSecrets flag to configure)" -ForegroundColor Gray
}

# --------------------------------------------------
# Step 3: Verify Docker image builds locally
# --------------------------------------------------
if (-not $SkipBuild) {
    Write-Host "[3/5] Building Docker image locally (verification)..." -ForegroundColor Yellow
    Push-Location $SrcDir
    docker build -t dualmind-api -f DualMind.API/Dockerfile .
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Docker build failed!" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location
    Write-Host "  ✓ Docker image built successfully" -ForegroundColor Green
} else {
    Write-Host "[3/5] Skipping Docker build (use without -SkipBuild to verify)" -ForegroundColor Gray
}

# --------------------------------------------------
# Step 4: Deploy to Cloudflare
# --------------------------------------------------
Write-Host "[4/5] Deploying to Cloudflare Containers..." -ForegroundColor Yellow
Push-Location $CfWorkerDir
npx wrangler deploy
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Deployment failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "  ✓ Deployed successfully" -ForegroundColor Green

# --------------------------------------------------
# Step 5: Post-deploy verification
# --------------------------------------------------
Write-Host "[5/5] Verifying deployment..." -ForegroundColor Yellow
Write-Host ""

$deployUrl = "https://dualmind-api.workers.dev"
if ($CustomDomain) {
    $deployUrl = "https://$CustomDomain"
}

Write-Host "  Waiting 10s for container to start..." -ForegroundColor Gray
Start-Sleep -Seconds 10

# Test health endpoint
try {
    $healthResponse = Invoke-RestMethod -Uri "$deployUrl/health" -Method GET -TimeoutSec 30
    Write-Host "  ✓ Health check passed: $($healthResponse.status)" -ForegroundColor Green
} catch {
    Write-Host "  ⚠ Health check failed (container may still be starting): $_" -ForegroundColor Yellow
    Write-Host "    Try again in 30s: curl $deployUrl/health" -ForegroundColor Gray
}

# Test Swagger
try {
    $swaggerResponse = Invoke-WebRequest -Uri "$deployUrl/swagger/index.html" -Method GET -TimeoutSec 30
    if ($swaggerResponse.StatusCode -eq 200) {
        Write-Host "  ✓ Swagger UI accessible" -ForegroundColor Green
    }
} catch {
    Write-Host "  ⚠ Swagger not accessible yet (may need ENABLE_SWAGGER=true)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  API:     $deployUrl" -ForegroundColor Cyan
Write-Host "  Health:  $deployUrl/health" -ForegroundColor Cyan
Write-Host "  Swagger: $deployUrl/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Custom domain: https://$CustomDomain" -ForegroundColor Cyan
Write-Host "  (Configure DNS: CNAME $CustomDomain -> dualmind-api.workers.dev)" -ForegroundColor Gray
Write-Host ""
