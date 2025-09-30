# GGs Platform - Admin License Generator
# This generates a valid admin license for testing

$ErrorActionPreference = "Stop"

# License payload
$payload = @{
    LicenseId = [Guid]::NewGuid().ToString()
    UserId = "admin@ggs.local"
    Tier = "Admin"
    IssuedUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    ExpiresUtc = (Get-Date).AddYears(5).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    Features = @("all", "admin", "remote", "analytics", "unlimited")
    MaxDevices = 999
    AllowOfflineValidation = $true
    IsPermanent = $true
    OrganizationName = "GGs Platform Admin"
    ContactEmail = "admin@ggs.local"
}

# Convert to JSON
$licenseJson = $payload | ConvertTo-Json -Compress

# For demo purposes, we'll use a simple signature
# In production, this would use proper RSA signing
$signature = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("ADMIN-SIGNATURE-2024"))

# Create signed license
$signedLicense = @{
    Payload = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($licenseJson))
    Signature = $signature
    Version = "1.0"
}

$finalLicense = $signedLicense | ConvertTo-Json -Compress

# Save to file
$outputPath = "$PSScriptRoot\..\ADMIN_LICENSE.txt"
$finalLicense | Out-File -FilePath $outputPath -Encoding UTF8

# Also create a more readable version
$readableLicense = @"
=================================================================
GGs PLATFORM - ADMIN LICENSE KEY
=================================================================

Copy the JSON below and paste it in the License Activation window:

$finalLicense

=================================================================
License Details:
- Type: Administrator (Full Access)
- Email: admin@ggs.local
- Expires: Never (Permanent License)
- Features: All features unlocked
- Max Devices: Unlimited
- Organization: GGs Platform Admin
=================================================================

To activate:
1. Launch GGs.Desktop.exe
2. Click "Activate License" 
3. Paste the JSON above
4. Click "Activate"
=================================================================
"@

$readableLicense | Out-File -FilePath "$PSScriptRoot\..\ADMIN_LICENSE_README.txt" -Encoding UTF8

Write-Host "Admin license generated successfully!" -ForegroundColor Green
Write-Host "License saved to: ADMIN_LICENSE.txt" -ForegroundColor Cyan
Write-Host "Instructions saved to: ADMIN_LICENSE_README.txt" -ForegroundColor Cyan

# Return the license for immediate use
return $finalLicense
