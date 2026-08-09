# ============================================
# Bulk upload .env secrets to GitHub Actions
# Usage: .\setup-github-secrets.ps1
# ============================================
# Prerequisites: gh CLI installed & authenticated
#   winget install GitHub.cli
#   gh auth login
# ============================================

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$EnvFile = Join-Path $ProjectRoot ".env"

if (-not (Test-Path $EnvFile)) {
    Write-Host "ERROR: .env file not found at $EnvFile" -ForegroundColor Red
    exit 1
}

# Map .env variable names → GitHub Secret names
# (some need renaming because .env uses different names than our CI/CD)
$secretMapping = @{
    "GROQ_API_KEY"                    = "GROQ_API_KEY"
    "GOOGLE_API_KEY"                  = "GOOGLE_API_KEY"
    "SUPABASE_URL"                    = "SUPABASE_URL"
    "SUPABASE_ANON_KEY"              = "SUPABASE_ANON_KEY"
    "SUPABASE_SERVICE_ROLE_KEY"      = "SUPABASE_SERVICE_ROLE_KEY"
    "Telegram__BotToken"             = "TELEGRAM_BOT_TOKEN"
    "CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID" = "CF_AI_GATEWAY_ACCOUNT_ID"
    "CLOUDFLARE_AI_GATEWAY_ID"       = "CF_AI_GATEWAY_ID"
    "CLOUDFLARE_AI_GATEWAY_TOKEN"    = "CF_AI_GATEWAY_TOKEN"
    "CLOUDFLARE_WORKERS_AI_API_TOKEN" = "CF_WORKERS_AI_API_TOKEN"
}

# Parse .env file
$envVars = @{}
Get-Content $EnvFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#")) {
        $parts = $line -split "=", 2
        if ($parts.Length -eq 2) {
            $envVars[$parts[0].Trim()] = $parts[1].Trim()
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Uploading secrets to GitHub Actions" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check gh CLI
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: gh CLI not installed. Run: winget install GitHub.cli" -ForegroundColor Red
    exit 1
}

$successCount = 0
$failCount = 0

foreach ($entry in $secretMapping.GetEnumerator()) {
    $envName = $entry.Key
    $ghName = $entry.Value
    $value = $envVars[$envName]

    if ([string]::IsNullOrEmpty($value)) {
        Write-Host "  SKIP  $ghName (not found in .env as $envName)" -ForegroundColor Yellow
        $failCount++
        continue
    }

    Write-Host "  SET   $ghName ... " -NoNewline
    $value | gh secret set $ghName 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "FAILED" -ForegroundColor Red
        $failCount++
    }
}

# Prompt for secrets NOT in .env
Write-Host ""
Write-Host "--- Manual secrets (not in .env) ---" -ForegroundColor Cyan

# Cloudflare Account ID (same as AI Gateway Account ID)
$cfAccountId = $envVars["CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID"]
if ($cfAccountId) {
    Write-Host "  SET   CLOUDFLARE_ACCOUNT_ID ... " -NoNewline
    $cfAccountId | gh secret set CLOUDFLARE_ACCOUNT_ID 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Host "OK" -ForegroundColor Green; $successCount++ }
    else { Write-Host "FAILED" -ForegroundColor Red; $failCount++ }
}

# Cloudflare API Token — must be entered manually
Write-Host ""
Write-Host "  CLOUDFLARE_API_TOKEN is not in .env." -ForegroundColor Yellow
Write-Host "  Create it at: https://dash.cloudflare.com/profile/api-tokens" -ForegroundColor Gray
Write-Host "  Use template: 'Edit Cloudflare Workers'" -ForegroundColor Gray
$apiToken = Read-Host "  Paste your Cloudflare API Token (or press Enter to skip)"
if ($apiToken) {
    $apiToken | gh secret set CLOUDFLARE_API_TOKEN 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  SET   CLOUDFLARE_API_TOKEN ... OK" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "  SET   CLOUDFLARE_API_TOKEN ... FAILED" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Done! $successCount secrets set, $failCount skipped/failed" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Now push to main to trigger deploy:" -ForegroundColor Cyan
Write-Host "    git add . && git commit -m 'feat: CF Containers deploy' && git push" -ForegroundColor White
Write-Host ""
