# Test Script for Pakistan Administrative Data Update
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5073'

function Invoke-Test($name, $url, $expectStatus) {
    try {
        $r = Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop
        $status = 200
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        $r = $null
    }
    $pass = if ($status -eq $expectStatus) { 'PASS' } else { 'FAIL' }
    Write-Host "[$pass] $name (HTTP $status, expected $expectStatus)"
    return $r
}

Write-Host '=== PROVINCE TESTS ==='
$provinces = Invoke-Test 'Get all provinces' "$base/api/locations/provinces" 200
Write-Host "  Province count: $($provinces.Count)"
$provinces | ForEach-Object { Write-Host "  [$($_.id)] $($_.name) ($($_.nameUrdu))" }

Write-Host "`n=== PUNJAB DISTRICTS ==="
$punjabId = ($provinces | Where-Object { $_.name -eq 'Punjab' }).id
Write-Host "  Punjab ID: $punjabId"
$punjabDistricts = Invoke-Test "Punjab districts" "$base/api/locations/provinces/$punjabId/districts" 200
Write-Host "  Punjab district count: $($punjabDistricts.Count)"
$punjabDistricts | ForEach-Object { Write-Host "  [$($_.id)] $($_.name)" }

Write-Host "`n=== OTHER PROVINCES ==="
foreach ($p in $provinces) {
    $districts = Invoke-RestMethod -Uri "$base/api/locations/provinces/$($p.id)/districts" -Method Get
    Write-Host "  $($p.name): $($districts.Count) districts"
}

Write-Host "`n=== RAWALPINDI TEHSILS ==="
$rawalpindiDistId = ($punjabDistricts | Where-Object { $_.name -eq 'Rawalpindi' }).id
Write-Host "  Rawalpindi district ID: $rawalpindiDistId"
$rawalTehsils = Invoke-Test "Rawalpindi tehsils" "$base/api/locations/districts/$rawalpindiDistId/tehsils" 200
Write-Host "  Rawalpindi tehsil count: $($rawalTehsils.Count)"
$rawalTehsils | ForEach-Object { Write-Host "  [$($_.id)] $($_.name)" }

Write-Host "`n=== LAHORE TEHSILS ==="
$lahoreDistId = ($punjabDistricts | Where-Object { $_.name -eq 'Lahore' }).id
$lahoreTehsils = Invoke-Test "Lahore tehsils" "$base/api/locations/districts/$lahoreDistId/tehsils" 200
Write-Host "  Lahore tehsil count: $($lahoreTehsils.Count)"
$lahoreTehsils | ForEach-Object { Write-Host "  [$($_.id)] $($_.name)" }

Write-Host "`n=== AUTH + FARM TESTS ==="
# Register user
$regBody = @{ FullName = 'Test User'; Email = 'test21@example.com'; Password = 'Test1234!'; ConfirmPassword = 'Test1234!' } | ConvertTo-Json
try {
    $reg = Invoke-RestMethod -Uri "$base/api/auth/register" -Method Post -ContentType 'application/json' -Body $regBody
    Write-Host "[PASS] Register"
} catch {
    Write-Host "[INFO] Register may already exist: $($_.Exception.Message)"
}
# Always login to get a token (register does not return a token)
$loginBody = @{ Identifier = 'test21@example.com'; Password = 'Test1234!' } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody
Write-Host "[PASS] Login - token received: $($login.token.Substring(0,20))..."
$token = $login.token
$headers = @{ Authorization = "Bearer $token" }

# Create farm with Rawalpindi
$rawalpindiTehsilId = ($rawalTehsils | Where-Object { $_.name -eq 'Rawalpindi' }).id
Write-Host "  Rawalpindi tehsil ID (for farm): $rawalpindiTehsilId"
$farmBody = @{
    FarmName = 'Test Farm 2.1'; ProvinceId = $punjabId; DistrictId = $rawalpindiDistId
    TehsilId = $rawalpindiTehsilId; FarmSize = 5.0; FarmSizeUnit = 'Acres'
} | ConvertTo-Json
try {
    $farm = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -ContentType 'application/json' -Body $farmBody -Headers $headers
    Write-Host "[PASS] Create farm (Rawalpindi): $($farm.farmName)"
    $farmId = $farm.id
} catch {
    Write-Host "[FAIL] Create farm: $($_.Exception.Message)"
}

# Get farms
$farms = Invoke-RestMethod -Uri "$base/api/farms" -Method Get -Headers $headers
Write-Host "[PASS] Get farms: $($farms.Count) farms"

# Create crop
if ($farmId) {
    $cropBody = @{ CropName = 'Wheat'; Season = 'Rabi'; Status = 'Active' } | ConvertTo-Json
    $crop = Invoke-RestMethod -Uri "$base/api/farms/$farmId/crops" -Method Post -ContentType 'application/json' -Body $cropBody -Headers $headers
    Write-Host "[PASS] Create crop: $($crop.cropName)"
}

Write-Host "`n=== SUMMARY ==="
Write-Host "Provinces: $($provinces.Count)"
Write-Host "Punjab districts: $($punjabDistricts.Count)"
