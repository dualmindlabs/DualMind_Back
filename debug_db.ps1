
$Url = "https://calqfzajyidkdzbaswjp.supabase.co/rest/v1"
$Key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImNhbHFmemFqeWlka2R6YmFzd2pwIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjQyNzMwODMsImV4cCI6MjA3OTg0OTA4M30.ptXyUNCcAhGi9u2kVDHOxSBvQv0W72S5HHqkIFXQS08"
$ThreadId = "da475263-e7ec-4879-9a35-b176e319cbcd"

$headers = @{
    "apikey" = $Key
    "Authorization" = "Bearer $Key"
}

Write-Host "--- Checking System Settings ---"
try {
    $settings = Invoke-RestMethod -Uri "$Url/system_settings?key=eq.public_sharing" -Method Get -Headers $headers
    $settings | ConvertTo-Json
} catch {
    Write-Error $_
}

Write-Host "`n--- Checking Thread Visibility ---"
try {
    $thread = Invoke-RestMethod -Uri "$Url/threads?thread_id=eq.$ThreadId&select=visibility,title" -Method Get -Headers $headers
    $thread | ConvertTo-Json
} catch {
    Write-Error $_
}
