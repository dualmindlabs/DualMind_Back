$baseUrl = "http://localhost:65476/api/admin"

function Log-Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Log-Pass($msg) { Write-Host "[PASS] $msg" -ForegroundColor Green }
function Log-Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red }

try {
    # 1. LIST PROVIDERS
    Log-Info "1. GET /providers"
    $providersRef = Invoke-RestMethod -Uri "$baseUrl/providers" -Method Get
    if ($providersRef.success -eq $true) {
        $count = $providersRef.data.Count
        Log-Pass "Found $count providers."
    } else {
        Log-Fail "Failed to list providers: $($providersRef.message)"
        exit
    }

    # 2. CREATE PROVIDER
    $testName = "test_prov_" + (New-Guid).ToString().Substring(0, 6)
    Log-Info "2. POST /providers (Name: $testName)"
    $body = @{
        provider_name = $testName
        display_name = "Test Provider"
        is_enabled = $true
        priority = 50
    } | ConvertTo-Json

    $res = Invoke-RestMethod -Uri "$baseUrl/providers" -Method Post -Body $body -ContentType "application/json"
    if ($res.success -eq $true) {
        Log-Pass "Provider created successfully."
    } else {
        Log-Fail "Failed to create provider: $($res.message)"
        exit
    }

    # 3. UPDATE PROVIDER
    Log-Info "3. PUT /providers/$testName"
    $body = @{
        display_name = "Test Provider Updated"
        is_enabled = $false
        priority = 10
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/providers/$testName" -Method Put -Body $body -ContentType "application/json"
    if ($res.success -eq $true) {
        Log-Pass "Provider updated successfully."
    } else {
        Log-Fail "Failed to update provider."
        exit
    }

    # 4. ADD KEY
    Log-Info "4. POST /providers/$testName/keys"
    $keySecret = "sk-test-1234567890-abcdef"
    $body = @{
        api_key = $keySecret
        is_active = $true
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/providers/$testName/keys" -Method Post -Body $body -ContentType "application/json"
    
    $keyId = $null
    if ($res.success -eq $true) {
        $keyId = $res.data
        Log-Pass "Key added successfully. ID: $keyId"
    } else {
        Log-Fail "Failed to add key: $($res.message)"
        exit
    }

    # 5. LIST KEYS
    Log-Info "5. GET /providers/$testName/keys"
    $res = Invoke-RestMethod -Uri "$baseUrl/providers/$testName/keys" -Method Get
    if ($res.success -eq $true) {
        $target = $res.data | Where-Object { $_.KeyId -eq $keyId }
        if ($target) {
            $mask = $target.DisplayMask
            if ($target.EncryptedApiKey) {
                Log-Fail "SECURITY ALERT: EncryptedApiKey returned in API!"
            } else {
                Log-Pass "Key found. Mask: $mask"
            }
        } else {
            Log-Fail "Created key not found in list!"
        }
    } else {
        Log-Fail "Failed to list keys."
    }

    # 6. DELETE KEY
    Log-Info "6. DELETE /keys/$keyId"
    $res = Invoke-RestMethod -Uri "$baseUrl/keys/$keyId" -Method Delete
    if ($res.success -eq $true) {
        Log-Pass "Key deleted successfully."
    } else {
        Log-Fail "Failed to delete key."
    }

    Log-Info "Verification Complete."

} catch {
    Log-Fail "Exception: $_"
    Write-Host $_.ScriptStackTrace
}
